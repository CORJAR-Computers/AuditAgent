import urllib.request
from PIL import Image, ImageDraw
import io

def create_icon():
    # Create a base image (rounded rectangle or circle)
    size = (256, 256)
    img = Image.new('RGBA', size, (255, 255, 255, 0))
    draw = ImageDraw.Draw(img)
    
    # Draw a rounded rect as background
    radius = 40
    # draw background: dark blue
    draw.rounded_rectangle([0, 0, 256, 256], radius=radius, fill=(30, 60, 114, 255))
    
    # Draw an 'A' for AuditAgent
    # draw text or shape... Let's just draw some polygons for a stylized 'A' or Shield.
    
    # Shield shape inside
    shield = [
        (128, 40), (196, 60), (196, 140),
        (128, 220), (60, 140), (60, 60)
    ]
    draw.polygon(shield, fill=(255, 255, 255, 255))
    
    # Inner blue shield
    inner_shield = [
        (128, 60), (176, 75), (176, 135),
        (128, 195), (80, 135), (80, 75)
    ]
    draw.polygon(inner_shield, fill=(42, 82, 152, 255))
    
    # Save as ICO
    icon_sizes = [(16,16), (24,24), (32,32), (48,48), (64,64), (128,128), (256,256)]
    img.save('src/AuditAgent.GUI/AuditAgent.ico', format='ICO', sizes=icon_sizes)
    print("Icon generated successfully!")

if __name__ == '__main__':
    create_icon()
