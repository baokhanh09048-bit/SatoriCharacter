extends Control

@onready var artwork: TextureRect = $TextureRect
@onready var video: VideoStreamPlayer = $VideoStreamPlayer
@onready var readability: ColorRect = $Readability

func _ready() -> void:
    video.visible = false
    _process(0.0)

func _process(_delta: float) -> void:
    # Compensate for the native AnimatedBg overscan. Keep the video aspect ratio
    # and center-cover the viewport; a matching poster hides decoder startup.
    var canvas := get_global_transform_with_canvas()
    var viewport := get_viewport().get_visible_rect()
    var canvas_scale := canvas.get_scale().abs()
    artwork.position = canvas.affine_inverse() * viewport.position
    artwork.size = viewport.size / canvas_scale
    readability.position = artwork.position
    readability.size = artwork.size
    var video_size := Vector2(1280, 720) * maxf(viewport.size.x / 1280.0, viewport.size.y / 720.0)
    video.position = canvas.affine_inverse() * (viewport.position + (viewport.size - video_size) * 0.5)
    video.size = video_size / canvas_scale
    video.paused = not is_visible_in_tree()
    # Keeping the last decoded frame over the poster also avoids a flash when
    # the native looping player resets stream_position to zero.
    if video.stream_position > 0.0:
        video.visible = true

func _exit_tree() -> void:
    video.stop()
