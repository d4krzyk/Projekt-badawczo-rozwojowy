import requests
import bs4
import typing
import re
from utils import get_headers

html_url = 'https://en.wikipedia.org/api/rest_v1/page/html/'


def text_or_link(c):
    if c.name == 'a':
        return {'class': 'link', 'text': c.string, 'href': c.get('href', '')}
    else:
        return {'class': 'text', 'value': c.string}


def extract_above(above_child: bs4.BeautifulSoup):
    above = []
    for c in above_child.contents:
        if c.name == 'div' and next(c.stripped_strings, None):
            above.append({'class': 'text', 'value': next(c.stripped_strings)})
        elif c.name == 'i' and next(c.stripped_strings, None):
            above.append({'class': 'text', 'value': next(c.stripped_strings)})
        elif c.name == 'b' and next(c.stripped_strings, None):
            above.append({'class': 'text', 'value': next(c.stripped_strings)})
        elif c.name == 'span' and next(c.stripped_strings, None):
            above.append({'class': 'text', 'value': next(c.stripped_strings)})
        elif not c.name and next(c.stripped_strings, None):
            above.append({'class': 'text', 'value': next(c.stripped_strings)})
    return above


def extract_header(header_child: bs4.BeautifulSoup):
    return [{'class': 'text', 'value': header_child.string}]


def extract_image(image_child: bs4.BeautifulSoup):
    image_link = image_child.find_all(
        class_='mw-file-description')[0].get('href', '')
    caption = image_child.find_all(class_='infobox-caption')

    caption_out = []
    if caption:
        for c in caption[0].contents:
            caption_out.append(text_or_link(c))
    return [{'class': 'link', 'href': image_link, 'caption': caption_out}]


def extract_full_data(full_data_child: bs4.BeautifulSoup):
    return []


def extract_label(label_child: bs4.BeautifulSoup):
    return {'class': 'text', 'value': label_child.string}


def extract_data(data_child: bs4.BeautifulSoup):
    output = []
    for c in data_child.contents:
        if c.name:
            if c.name == 'a':
                output.append(text_or_link(c))
        else:
            output.append({'class': 'text', 'value': c.string})
    return output


def extract_infobox(infobox_soup):
    infobox_values = []
    for tr in infobox_soup.find_all('tr'):
        infobox_value = {}
        for child in tr.children:
            child_class = child.get('class', [])

            if len(child_class) == 0:
                continue

            child_class = child_class[0]

            if child_class == 'infobox-above':
                infobox_value['class'] = 'above'
                infobox_value['value'] = extract_above(child)
            elif child_class == 'infobox-subheader':
                continue
            elif child_class == 'infobox-header':
                infobox_value['class'] = 'header'
                infobox_value['value'] = extract_header(child)
            elif child_class == 'infobox-image':
                infobox_value['class'] = 'image'
                infobox_value['value'] = extract_image(child)
            elif child_class == 'infobox-full-data':
                infobox_value['class'] = 'full-data'
                infobox_value['value'] = extract_full_data(child)
            elif child_class == 'infobox-label':
                infobox_value['label'] = extract_label(child)
            elif child_class == 'infobox-data':
                infobox_value['class'] = 'data'
                infobox_value['value'] = extract_data(child)
            else:
                print(child_class)

        if len(infobox_value) != 0:
            infobox_values.append(infobox_value)

    return infobox_values


def find_infoboxes(content):
    soup = bs4.BeautifulSoup(content, features='html.parser')
    infoboxes = soup.find_all(class_='infobox')
    infoboxse_parsed = []
    for infobox in infoboxes:
        infoboxse_parsed.append(extract_infobox(infobox))

    return infoboxse_parsed


def extract_infoboxes(page_name):
    html_content = requests.get(
        html_url + page_name, headers=get_headers()).content

    return find_infoboxes(html_content)
