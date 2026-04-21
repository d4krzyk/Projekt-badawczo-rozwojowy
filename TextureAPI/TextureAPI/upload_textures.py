import json
import httpx
import asyncio
from pathlib import Path
import base64

# Configuration
JSON_FILE = Path("output/textures_30.json")
API_URL = "http://wikirooms.duckdns.org/cache/cache_texture"  # Remote endpoint
TIMEOUT = 30

# Authentication
USERNAME = ""
PASSWORD = ""
AUTH_TUPLE = (USERNAME, PASSWORD)
AUTH_HEADER = base64.b64encode(f"{USERNAME}:{PASSWORD}".encode()).decode()
HEADERS = {"Authorization": f"Basic {AUTH_HEADER}"}

async def upload_textures():
    """Read textures from JSON and upload them via API"""
    
    # Load JSON file
    with open(JSON_FILE, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    total_textures = 0
    successful_uploads = 0
    failed_uploads = 0
    
    # Iterate through categories
    async with httpx.AsyncClient(timeout=TIMEOUT, auth=AUTH_TUPLE) as client:
        for category, textures in data.items():
            print(f"\nProcessing category: {category}")
            
            for texture in textures:
                total_textures += 1
                
                # Prepare the payload
                payload = {
                    "texture_wall": texture.get("texture_wall"),
                    "texture_floor": texture.get("texture_floor"),
                    "texture_bookcase": texture.get("texture_bookcase"),
                    "article": texture.get("texture_id", ""),  # Use texture_id as article name
                    "category": category
                }
                
                try:
                    # Send POST request
                    response = await client.post(API_URL, json=payload)
                    response.raise_for_status()
                    
                    result = response.json()
                    if result.get("status") == "OK":
                        successful_uploads += 1
                        print(f"✓ Uploaded: {texture.get('texture_id')}")
                    else:
                        failed_uploads += 1
                        print(f"✗ Failed: {texture.get('texture_id')} - {result.get('message', 'Unknown error')}")
                
                except httpx.RequestError as e:
                    failed_uploads += 1
                    print(f"✗ Error uploading {texture.get('texture_id')}: {e}")
                except Exception as e:
                    failed_uploads += 1
                    print(f"✗ Unexpected error for {texture.get('texture_id')}: {e}")
    
    # Print summary
    print("\n" + "="*60)
    print(f"Upload Summary:")
    print(f"  Total textures: {total_textures}")
    print(f"  Successful: {successful_uploads}")
    print(f"  Failed: {failed_uploads}")
    print("="*60)


def main():
    """Main entry point"""
    try:
        asyncio.run(upload_textures())
    except KeyboardInterrupt:
        print("\n\nUpload cancelled by user.")
    except Exception as e:
        print(f"\nFatal error: {e}")


if __name__ == "__main__":
    main()
