# Standard Library
import json

# 3rd-Party
import requests


def get_headers():
    return {
        "User-Agent": (
            "RoomPedia/1.0 (https://github.com/d4krzyk/Projekt-badawczo-rozwojowy; "
            "email: 313008@stud.umk.pl) requests/2.32.3"
        )
    }


redirect_url = 'https://en.wikipedia.org/w/rest.php/v1/search/page?limit=1&q='


def get_redirect_name(page_name: str) -> str:
    json_content = requests.get(
        redirect_url + page_name, headers=get_headers()).content
    json_object = json.loads(json_content)

    return json_object['pages'][0]['title']
