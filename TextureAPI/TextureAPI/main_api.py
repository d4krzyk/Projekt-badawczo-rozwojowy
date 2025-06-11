from fastapi import FastAPI
from pydantic import BaseModel

import prompt_config
from generator_api import generate_all_images

genModel = prompt_config.TextureModel(mode="cuda")
app = FastAPI()


class CategoryInput(BaseModel):
    category: str

@app.post("/gen2DTextures")
def generate_endpoint(input: CategoryInput):
    types = ["wall", "floor", "bookcase"]
    category = input.category
    category_clean = category.replace(" ", "").upper()
    if category_clean not in genModel.prompt_data:
        return {"error": f"Invalid category: '{category}'"}
    images = generate_all_images(types,str(category_clean), model=genModel)
    return {"images": images}