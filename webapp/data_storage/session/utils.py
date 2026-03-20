WIKIPEDIA_PREFIX = "https://en.wikipedia.org/wiki/"


def strip_wikipedia_prefix(value: str) -> str:
    return value.removeprefix(WIKIPEDIA_PREFIX)
