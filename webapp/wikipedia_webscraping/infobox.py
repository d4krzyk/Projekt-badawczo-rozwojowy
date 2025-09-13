import requests
import bs4
import typing
import re

NON_BREAKING_ELEMENTS = ['a', 'abbr', 'acronym', 'audio', 'b', 'bdi', 'bdo', 'big', 'button',
                         'canvas', 'cite', 'code', 'data', 'datalist', 'del', 'dfn', 'em', 'embed', 'i', 'iframe',
                         'img', 'input', 'ins', 'kbd', 'label', 'map', 'mark', 'meter', 'noscript', 'object', 'output',
                         'picture', 'progress', 'q', 'ruby', 's', 'samp', 'script', 'select', 'slot', 'small', 'span',
                         'strong', 'sub', 'sup', 'svg', 'template', 'textarea', 'time', 'u', 'tt', 'var', 'video', 'wbr']

html_url = 'https://en.wikipedia.org/api/rest_v1/page/html/'


def get_text(tag: bs4.Tag) -> str:

    def _get_text(tag: bs4.Tag) -> typing.Generator:
        for child in tag.children:
            if isinstance(child, bs4.Tag):
                is_block_element = child.name not in NON_BREAKING_ELEMENTS
                if is_block_element:
                    yield "\n"
                yield from ["\n"] if child.name == "br" else _get_text(child)
                if is_block_element:
                    yield "\n"
            elif isinstance(child, bs4.NavigableString):
                yield child.string

    return "".join(_get_text(tag))


def extract_infobox(page_name: str) -> {}:
    def extract_data(soup):
        found_li = soup.find_all('li')

        if found_li:
            output = []
            for li in found_li:
                data = "".join(re.sub(r"\n+", '\n',
                                      re.sub(r"\[[^\[^\]]+\]", '',
                                             get_text(li).strip(), flags=re.A), flags=re.A)).split("\n")
                if len(data) == 1:
                    output.append(data[0])
                else:
                    output.append(data)
            return output

        if len(soup.contents) > 1:
            text = re.sub(r"\[[^\[\]]+\]", '',
                          ''.join(get_text(soup).strip()), flags=re.A)
            text = re.sub(r"\n+", '\n', text, flags=re.A).split("\n")

            return text

        return soup.string

    output = {}
    html_content = requests.get(
        html_url + page_name).content
    soup = bs4.BeautifulSoup(html_content, features='html.parser')
    table = soup.find('table', class_='infobox')

    for row in table.find_all('tr'):
        label = row.find(class_="infobox-label")
        data = row.find(class_="infobox-data")

        if label:
            output[label.string] = extract_data(data)

    return output
