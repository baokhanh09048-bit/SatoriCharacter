extends Node2D

const MANIFEST := "res://SatoriCharacter/act4_animation_manifest.json"
@export var character_key: String = "koishi"
@export var display_height: float = 340.0
var clock_time: float = 0.0
var action_time: float = 0.0
var action: String = "Idle"
var action_duration: float = 0.0
var art_scale: float = 1.0
var is_conceding: bool = false
var unseen: bool = false
var charge: int = 0
var state: String = ""
var pending_clip: String = "idle"
var followup: String = ""
var transition_tween: Tween
var frame_origin: Vector2
var clips: Dictionary = {}
@onready var artwork: Sprite2D = $Artwork
@onready var frames: AnimatedSprite2D = $Frames
@onready var transition: Sprite2D = $Transition
static var shared_manifest: Dictionary = {}
static var shared_clips: Dictionary = {}
static var loading: Dictionary = {}

func _ready() -> void:
    unseen = character_key == "koishi"
    clock_time = 0.4 if character_key == "orin" else 1.8 if character_key == "okuu" else 0.0
    if shared_manifest.is_empty() and FileAccess.file_exists(MANIFEST):
        var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST))
        if parsed is Dictionary:
            shared_manifest = parsed
    clips = shared_manifest.get("characters", {}).get(character_key, {})
    frames.sprite_frames = SpriteFrames.new()
    var canvas: Dictionary = shared_manifest.get("frame_canvas", {"width":240,"height":280})
    var ratio := display_height / float(shared_manifest.get("visible_reference_height",228.125))
    frames.scale = Vector2.ONE * ratio
    frames.position.y = (float(canvas.height) / 2.0 - float(shared_manifest.get("feet_baseline_y",263.125))) * ratio
    frame_origin = frames.position
    var poster: String = clips.get("idle",{}).get("poster","res://SatoriCharacter/assets/act4/" + character_key + ".png")
    artwork.texture = load(poster)
    art_scale = ratio
    artwork.scale = Vector2.ONE * art_scale
    artwork.position = frame_origin
    transition.scale = frames.scale
    transition.position = frames.position
    frames.animation_finished.connect(_finished)
    _request("idle")
    for clip_name: String in clips.keys():
        _request(clip_name)

func _request(clip_name: String) -> void:
    var key := character_key + "/" + clip_name
    if shared_clips.has(key) or loading.has(key) or not clips.has(clip_name):
        return
    var info: Dictionary = clips[clip_name]
    loading[key] = {"info":info,"textures":{},"failed":false}
    for path: String in info.atlases:
        if ResourceLoader.load_threaded_request(path,"Texture2D") != OK:
            loading[key].failed = true
            push_warning("Satori garden: failed atlas request " + path)

func _handoff() -> void:
    # One completed page per actor per frame; no decoding on a card-play trigger.
    for key: String in loading.keys():
        if not key.begins_with(character_key + "/"):
            continue
        var job: Dictionary = loading[key]
        if job.failed:
            loading.erase(key)
            continue
        var accepted := false
        for path: String in job.info.atlases:
            if job.textures.has(path):
                continue
            var status := ResourceLoader.load_threaded_get_status(path)
            if status == ResourceLoader.THREAD_LOAD_LOADED and not accepted:
                job.textures[path] = ResourceLoader.load_threaded_get(path)
                accepted = true
            elif status in [ResourceLoader.THREAD_LOAD_FAILED,ResourceLoader.THREAD_LOAD_INVALID_RESOURCE]:
                job.failed = true
                push_warning("Satori garden: failed atlas load " + path)
        if job.textures.size() == job.info.atlases.size():
            var library := SpriteFrames.new()
            library.add_animation(key)
            library.set_animation_speed(key,float(job.info.get("fps",60)))
            library.set_animation_loop(key,bool(job.info.get("loop",false)))
            for descriptor: Dictionary in job.info.frames:
                var texture := AtlasTexture.new()
                texture.atlas = job.textures[job.info.atlases[int(descriptor.atlas)]]
                var rect: Array = descriptor.region
                texture.region = Rect2(rect[0],rect[1],rect[2],rect[3])
                texture.filter_clip = true
                library.add_frame(key,texture)
            shared_clips[key] = library
            loading.erase(key)
        if accepted:
            return

func _play(clip_name: String) -> bool:
    var key := character_key + "/" + clip_name
    if not shared_clips.has(key):
        _request(clip_name)
        pending_clip = clip_name
        return false
    var library := frames.sprite_frames
    if not library.has_animation(key):
        var source: SpriteFrames = shared_clips[key]
        library.add_animation(key)
        library.set_animation_speed(key,source.get_animation_speed(key))
        library.set_animation_loop(key,source.get_animation_loop(key))
        for index in range(source.get_frame_count(key)):
            library.add_frame(key,source.get_frame_texture(key,index))
    if transition_tween and transition_tween.is_valid():
        transition_tween.kill()
    transition.visible = false
    if frames.visible and frames.sprite_frames.has_animation(frames.animation):
        transition.texture = frames.sprite_frames.get_frame_texture(frames.animation,frames.frame)
        transition.modulate = Color(1,1,1,1)
        transition.visible = true
        transition_tween = create_tween()
        transition_tween.tween_property(transition,"modulate:a",0.0,0.08)
        transition_tween.tween_callback(func(): transition.hide())
    state = clip_name
    pending_clip = ""
    artwork.hide()
    frames.show()
    frames.stop()
    frames.play(key)
    return true

func _finished() -> void:
    if is_conceding:
        frames.stop()
        frames.frame = frames.sprite_frames.get_frame_count(frames.animation)-1
        return
    if not followup.is_empty():
        var next := followup
        followup = ""
        _play(next)
    else:
        _play("idle")

func garden_trigger(trigger: String) -> void:
    if character_key == "orin":
        return # One loop only; aid now comes from playable cards.
    if is_conceding:
        return
    if trigger == "PresenceBroken":
        unseen = false
        trigger = "Reveal"
    var next := ""
    match trigger:
        "Dead", "Concede":
            is_conceding = true
            next = "concede"
        "Attack": next = "attack"
        "Cast": next = "charge" if character_key == "okuu" else "cast"
        "Hit": next = "hit"
        "Reveal": next = "reveal"
        "Protect": next = "protect"
        "Idle":
            if state.is_empty() or state == "idle": next = "idle"
    if next.is_empty():
        return
    if next == state and frames.is_playing() and (next == "idle" or action_time < 0.15):
        return
    action = trigger
    action_time = 0.0
    action_duration = float(clips.get(next,{}).get("seconds",0.95))
    followup = ""
    _play(next)

func garden_charge(value: int) -> void:
    charge = value
    if value > 0:
        garden_trigger("Cast")

func garden_set_unseen(value: bool) -> void:
    unseen = value

func _process(delta: float) -> void:
    clock_time += delta
    action_time += delta
    _handoff()
    if not pending_clip.is_empty() and shared_clips.has(character_key + "/" + pending_clip):
        _play(pending_clip)
    var alpha := 0.62 + sin(clock_time * 1.8) * 0.08 if unseen else 1.0
    if action == "Reveal" and action_time < action_duration:
        alpha = 0.55 + 0.45 * absf(cos(action_time * 9.0))
    frames.self_modulate.a = alpha
    artwork.self_modulate.a = alpha
    transition.self_modulate.a = alpha
    # A continuous hover belongs to the drawing, never to combat timing or hitboxes.
    var hover := -20.0 + sin(clock_time * 1.45) * 10.0 if character_key == "koishi" else 0.0
    frames.position = frame_origin + Vector2(0.0,hover)
    transition.position = frames.position
    if artwork.visible:
        var breathe := sin(clock_time * 2.4)
        var squash := Vector2(1.0-breathe*0.003,1.0+breathe*0.004)
        artwork.scale = squash * art_scale
        artwork.position.y = frame_origin.y + hover - display_height * (squash.y-1.0) / 2.0
    if character_key in ["okuu","orin"]:
        queue_redraw()

func _draw() -> void:
    if character_key == "okuu":
        for orb in range(2):
            draw_circle(Vector2((orb-0.5)*17.0,-display_height-14.0),5.0,Color(1.0,0.73,0.27,0.9 if orb<charge else 0.2))
    if character_key == "orin":
        for index in range(2):
            var pos := Vector2(-70.0 if index==0 else 78.0,-display_height*0.60+sin(clock_time*2.5+index*2.0)*10.0)
            draw_circle(pos,20.0,Color(0.25,0.45,1.0,0.15))
            draw_circle(pos,12.0,Color(0.45,0.75,1.0,0.62))
            draw_circle(pos+Vector2(-3.0,-2.0),2.0,Color(0.95,1.0,1.0,0.9))
            draw_circle(pos+Vector2(3.0,-2.0),2.0,Color(0.95,1.0,1.0,0.9))
            for flame in range(3):
                var origin := pos+Vector2((flame-1)*6.0,-9.0)
                draw_line(origin,origin+Vector2(sin(clock_time*3.0+flame)*6.0,-17.0-sin(clock_time*4.0+flame)*6.0),Color(0.4,0.65,1.0,0.65),3.0,true)
