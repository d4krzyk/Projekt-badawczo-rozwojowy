from sqlalchemy.orm import Session
from sqlalchemy.sql.expression import func
from wikipedia_cache.models import Texture, TextureCache
from wikipedia_cache.repository import TextureCacheRepository, TextureRepository


def save_texture_to_db(data: dict, db: Session):
    cacheRepo = TextureCacheRepository(db)
    textureRepo = TextureRepository(db)
    new_texture = textureRepo.create(
        texture_wall=data["texture_wall"],
        texture_floor=data["texture_floor"],
        texture_bookcase=data["texture_bookcase"]
    )
    new_textureCache = cacheRepo.create(
        article_name=data["article"],
        category=data["category"],
        texture_id=new_texture.id
    )
    db.commit()
    db.refresh(new_textureCache)
    db.refresh(new_texture)
    return new_textureCache

def get_cached_texture(article: str, category: str, db: Session):
    texture_cache = db.query(TextureCache).filter_by(article_name=article).first()
    if not texture_cache:
        texture_cache = db.query(TextureCache).filter_by(category=category).order_by(func.random()).first()
        if not texture_cache:
            return None
    texture = db.query(Texture).filter_by(id=texture_cache.texture_id).first()
    return {
        'texture_wall': texture.texture_wall,
        'texture_floor': texture.texture_floor,
        'texture_bookcase': texture.texture_bookcase
    }
