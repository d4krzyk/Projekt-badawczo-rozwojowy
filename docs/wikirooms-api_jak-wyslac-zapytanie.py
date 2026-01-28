# !pip install curlify requests

import requests
import base64
import curlify

URL = "http://wikirooms.duckdns.org/"

USERNAME = "projektBR"
PASSWORD = USERNAME.swapcase()

AUTH_TUPLE = (USERNAME, PASSWORD)
AUTH_HEADER = base64.b64encode(f"{USERNAME}:{PASSWORD}".encode()).decode()
HEADERS = {"Authorization": f"Basic {AUTH_HEADER}"}

response = requests.get(URL, headers=HEADERS)

print(response.text)

print(curlify.to_curl(response.request))
