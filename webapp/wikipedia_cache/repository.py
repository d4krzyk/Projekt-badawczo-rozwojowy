from sqlalchemy.orm import Session
from .models import TextureCache, Texture

class TextureCacheRepository:
    def __init__(self, db: Session):
        self.db = db

    def create(self, article_name: str, category: str, texture_id: int) -> TextureCache:
        textureCache = TextureCache(article_name=article_name, category=category, texture_id=texture_id)
        self.db.add(textureCache)
        self.db.flush()
        return textureCache

class TextureRepository:
    def __init__(self, db: Session):
        self.db = db

    def create(self, texture_wall: str, texture_floor: str, texture_bookcase: str) -> Texture:
        texture = Texture(texture_wall=texture_wall, texture_floor=texture_floor, texture_bookcase=texture_bookcase)
        self.db.add(texture)
        self.db.flush()
        return texture
