"""Draws icon.png from the same geometry as icon.svg.

Not an SVG renderer: the shapes are hand-drawn with the same numbers, at eight
times the size, then reduced. That is what gives the clean edges, and it is why
the two files have to be changed together.

Two places need care. A stroke straddles its path in SVG, so an outlined circle
of radius 17 and width 7 runs from radius 13.5 to 20.5, and Pillow grows an
outline inwards from the bounding box — the box has to be the outer radius. And
Pillow has no round line caps, so the arrow's shaft is a rounded rectangle and
its head gets a disc at each open end.
"""
from PIL import Image, ImageDraw

S = 8
N = 128
NAVY = (14, 43, 69, 255)
MINT = (94, 234, 212, 255)
AMBER = (251, 191, 36, 255)

img = Image.new("RGBA", (N * S, N * S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)


def px(v):
    return round(v * S)


d.rounded_rectangle([0, 0, px(N) - 1, px(N) - 1], radius=px(28), fill=NAVY)

# The source: a filled rounded square.
d.rounded_rectangle([px(12), px(48), px(44), px(80)], radius=px(9), fill=MINT)

# The arrow. Shaft: a 6-wide round-capped line from (51, 64) to (66, 64).
d.rounded_rectangle([px(48), px(61), px(69), px(67)], radius=px(3), fill=MINT)

# Head: (62, 57) to (69, 64) to (62, 71), round joined and round capped.
d.line([px(62), px(57), px(69), px(64), px(62), px(71)],
       fill=MINT, width=px(6), joint="curve")
for x, y in ((62, 57), (62, 71)):
    d.ellipse([px(x - 3), px(y - 3), px(x + 3), px(y + 3)], fill=MINT)

# The destination: an outlined circle, radius 17, stroke 7, so 13.5 to 20.5.
d.ellipse([px(97 - 20.5), px(64 - 20.5), px(97 + 20.5), px(64 + 20.5)],
          fill=None, outline=AMBER, width=px(7))

img = img.resize((N, N), Image.LANCZOS)
img.save("icon.png", optimize=True)
print("icon.png escrito:", img.size, img.mode)
