extends Node2D

const ANIMATION_MANIFEST := "res://SatoriCharacter/animation_manifest.json"
const ROUTES := {
    "Idle":"idle_loop", "Relaxed":"relaxed_loop", "Attack":"attack",
    "Shiv":"attack", "Cast":"cast", "PowerUp":"powerup", "Defend":"defend",
    "Victory":"victory", "Hit":"hurt", "Dead":"die", "Revive":"revive",
    "RestEnter":"rest_enter", "RestIdle":"rest_idle", "RestSleep":"rest_sleep",
    "SleepIdle":"sleep_idle", "Wake":"wake", "RestExit":"rest_exit",
    "Upgrade":"upgrade", "Merchant":"merchant"
}
const FOLLOWUPS := {"rest_enter":"rest_idle", "rest_sleep":"sleep_idle",
    "wake":"rest_idle", "upgrade":"rest_idle", "rest_exit":"idle_loop"}
@export var scene_context := "battle"
@export var display_scale := 0.43

@onready var artwork: Sprite2D = $Artwork
@onready var frames: AnimatedSprite2D = $Frames
@onready var transition: Sprite2D = $Transition
var transition_motion: Tween
var state := ""
var manifest: Dictionary = {}
var loaded_order: Array[String] = []
var pending_exit := false
var pending_clip := ""
var max_load_handoff_usec := 0
# Keep the most useful animations alive between encounters. ResourceLoader's
# background requests decode/transcode atlas pages without blocking a trigger.
static var shared_clips: Dictionary = {}
static var shared_manifest: Dictionary = {}
static var shared_order: Array[String] = []
static var loading_clips: Dictionary = {}

func _ready() -> void:
    if shared_manifest.is_empty():
        var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(ANIMATION_MANIFEST))
        if parsed is Dictionary:
            shared_manifest = parsed
    manifest = shared_manifest
    frames.sprite_frames = SpriteFrames.new()
    var canvas: Dictionary = manifest.get("frame_canvas", {"width":320, "height":384})
    var display_canvas: Dictionary = manifest.get("display_reference_canvas", {"width":640, "height":768})
    frames.scale = Vector2.ONE * display_scale * float(display_canvas.width) / float(canvas.width) * float(manifest.get("display_scale_compensation",1.0))
    transition.scale = frames.scale
    artwork.scale = frames.scale
    frames.position = Vector2(0, (float(canvas.height)/2.0-float(manifest.get("feet_baseline_y",269.25))) * frames.scale.y-6.0*(display_scale/0.43))
    transition.position = frames.position
    artwork.position = frames.position
    frames.animation_finished.connect(_on_animation_finished)
    if scene_context == "battle":
        for clip: String in ["idle_loop", "attack", "defend"]:
            _request_clip(clip)
        for clip: String in ["hurt", "cast", "powerup"]:
            _request_clip(clip)
    add_to_group("satori_" + scene_context)
    match scene_context:
        "rest": satori_play_trigger("RestEnter")
        "merchant": satori_play_trigger("Merchant")
        _: satori_play_trigger("Idle")

func _request_clip(clip_name: String) -> void:
    if shared_clips.has(clip_name) or loading_clips.has(clip_name):
        return
    var clip: Dictionary = manifest.get("clips", {}).get(clip_name, {})
    if clip.is_empty():
        return
    loading_clips[clip_name] = {"info":clip, "textures":{}, "failed":false}
    for path: String in clip.get("atlases", []):
        if ResourceLoader.load_threaded_request(path, "Texture2D") != OK:
            loading_clips[clip_name].failed = true
            push_warning("Satori: atlas request failed " + path)

func _process(_delta: float) -> void:
    var started := Time.get_ticks_usec()
    # Limit the main-thread resource handoff to one page per frame. Native action
    # triggers never synchronously read hundreds of files or wait for a worker.
    var handed_off := false
    for clip_name: String in loading_clips.keys():
        var job: Dictionary = loading_clips[clip_name]
        if job.failed:
            loading_clips.erase(clip_name)
            continue
        for path: String in job.info.atlases:
            if job.textures.has(path):
                continue
            var status := ResourceLoader.load_threaded_get_status(path)
            if status == ResourceLoader.THREAD_LOAD_LOADED and not handed_off:
                job.textures[path] = ResourceLoader.load_threaded_get(path)
                handed_off = true
            elif status == ResourceLoader.THREAD_LOAD_FAILED or status == ResourceLoader.THREAD_LOAD_INVALID_RESOURCE:
                job.failed = true
                push_warning("Satori: atlas load failed " + path)
        if job.textures.size() == job.info.atlases.size():
            _complete_clip(clip_name, job)
            loading_clips.erase(clip_name)
        if handed_off:
            break
    if not pending_clip.is_empty() and shared_clips.has(pending_clip):
        var ready_clip := pending_clip
        pending_clip = ""
        _play_clip(ready_clip)
    if scene_context == "battle":
        for clip: String in ["idle_loop","attack","defend"]:
            if shared_clips.has(clip) and not frames.sprite_frames.has_animation(clip):
                _load_clip(clip)
        _trim_cache()
    max_load_handoff_usec = maxi(max_load_handoff_usec,Time.get_ticks_usec()-started)

func _complete_clip(clip_name: String, job: Dictionary) -> void:
    var clip: Dictionary = job.info
    var library := SpriteFrames.new()
    library.add_animation(clip_name)
    library.set_animation_speed(clip_name, float(clip.get("fps",60)))
    library.set_animation_loop(clip_name, bool(clip.get("loop",false)))
    var paths: Array = clip.frames
    var start_frame := int(clip.get("start_frame",0))
    for source_index in range(start_frame,paths.size()):
        var frame_info: Dictionary = paths[source_index]
        var texture := AtlasTexture.new()
        texture.atlas = job.textures[clip.atlases[int(frame_info.atlas)]]
        var rect: Array = frame_info.region
        texture.region = Rect2(rect[0],rect[1],rect[2],rect[3])
        texture.filter_clip = true
        var duration := 1.0
        if source_index < int(clip.get("quick_until_frame",0)):
            duration = float(clip.get("windup_duration",1.0))
        library.add_frame(clip_name,texture,duration)
    if clip.get("loop_mode","") == "ping_pong":
        for index in range(paths.size()-start_frame-2,0,-1):
            library.add_frame(clip_name,library.get_frame_texture(clip_name,index))
    shared_clips[clip_name] = library
    shared_order.erase(clip_name)
    shared_order.append(clip_name)
    while shared_order.size() > 10:
        var oldest: String = shared_order.pop_front()
        if oldest in ["idle_loop","attack","defend","cast","powerup","hurt"]:
            shared_order.append(oldest)
        else:
            shared_clips.erase(oldest)

func _load_clip(clip_name: String) -> bool:
    var library := frames.sprite_frames
    if library.has_animation(clip_name):
        loaded_order.erase(clip_name)
        loaded_order.append(clip_name)
        return true
    if not shared_clips.has(clip_name):
        _request_clip(clip_name)
        return false
    var source: SpriteFrames = shared_clips[clip_name]
    shared_order.erase(clip_name)
    shared_order.append(clip_name)
    library.add_animation(clip_name)
    library.set_animation_speed(clip_name,source.get_animation_speed(clip_name))
    library.set_animation_loop(clip_name,source.get_animation_loop(clip_name))
    for index in range(source.get_frame_count(clip_name)):
        library.add_frame(clip_name,source.get_frame_texture(clip_name,index),source.get_frame_duration(clip_name,index))
    loaded_order.append(clip_name)
    return true

func _trim_cache() -> void:
    # Bound resident sequence textures independently for each actor/scene.
    var capacity := 7 if scene_context == "battle" else 3
    while loaded_order.size() > capacity:
        var old_clip: String = loaded_order.pop_front()
        if old_clip == state or (scene_context == "battle" and old_clip in ["idle_loop", "attack", "defend", "cast", "powerup", "hurt"]):
            loaded_order.append(old_clip)
        else:
            frames.sprite_frames.remove_animation(old_clip)

func satori_play_trigger(trigger: String) -> void:
    # Shop overlays may repeat entry callbacks; never replay the walk-in clip.
    if scene_context == "merchant" and trigger == "Merchant" and (not state.is_empty() or not pending_clip.is_empty()):
        return
    if (state == "die" or pending_clip == "die") and trigger != "Revive":
        return
    if trigger == "RestExit" and state in ["rest_sleep", "sleep_idle"]:
        pending_exit = true
        if state == "rest_sleep" and frames.is_playing():
            return
        trigger = "Wake"
    if not ROUTES.has(trigger):
        return
    var next_clip: String = ROUTES[trigger]
    if trigger == "Idle" and not pending_clip.is_empty() and pending_clip not in ["idle_loop","relaxed_loop"]:
        return
    if next_clip == state and frames.is_playing() and frames.sprite_frames.get_animation_loop(state):
        return
    # Native idle updates must not cancel visible action/feedback mid-clip.
    if trigger == "Idle" and state not in ["", "idle_loop", "relaxed_loop"] and frames.is_playing():
        return
    _play_clip(next_clip)

func _play_clip(clip_name: String) -> void:
    if not _load_clip(clip_name):
        pending_clip = clip_name
        if state.is_empty():
            artwork.show()
            frames.hide()
        return
    pending_clip = ""
    # Prefetch the natural follow-up while the current clip plays.
    _request_clip(FOLLOWUPS.get(clip_name,"idle_loop"))
    if transition_motion != null and transition_motion.is_valid():
        transition_motion.kill()
    transition.hide()
    if frames.visible and frames.sprite_frames.has_animation(frames.animation):
        transition.texture = frames.sprite_frames.get_frame_texture(frames.animation, frames.frame)
        transition.modulate = Color.WHITE
        transition.show()
        transition_motion = create_tween()
        transition_motion.tween_property(transition, "modulate:a", 0.0, 0.04 if clip_name in ["attack", "defend"] else 0.08)
        transition_motion.finished.connect(func() -> void:
            transition.hide()
            transition.texture = null)
    state = clip_name
    frames.stop()
    frames.modulate = Color.WHITE
    artwork.hide()
    frames.show()
    frames.animation = state
    frames.speed_scale = float(manifest.clips[clip_name].get("speed_scale",1.0))
    frames.set_frame_and_progress(0, 0.0)
    frames.play()
    _trim_cache()

func _on_animation_finished() -> void:
    if state in ["die", "victory", "rest_exit"]:
        return
    if state == "rest_sleep" and pending_exit:
        _play_clip("wake")
        return
    if state == "wake" and pending_exit:
        pending_exit = false
        _play_clip("rest_exit")
        return
    _play_clip(FOLLOWUPS.get(state, "idle_loop"))
