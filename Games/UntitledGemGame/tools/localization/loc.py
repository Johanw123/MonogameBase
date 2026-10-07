#!/usr/bin/env python3
"""String tables for Beyond the Belt (see Localization/Loc.cs).

  python3 tools/localization/loc.py extract   # collect every key into Content/Localization/en.json
  python3 tools/localization/loc.py status    # per language: missing, stale and broken entries
  python3 tools/localization/loc.py sync      # order each table like en.json and drop stale entries
  python3 tools/localization/loc.py fonts     # subset the CJK/Thai fonts and rebuild their atlases

Keys are the English text. They come from string literals passed to Loc.T, Loc.F, Loc.N
and Loc.P in the code, the name/tooltip/tooltip_extra fields of Content/Data/upgrades*.json
and the Text values of the Gum screens. A Loc.P key is "one|other"; its translation lists
the language's plural forms (GameLanguage.PluralForms) separated by '|'.

`fonts` needs fontTools (pip install fonttools) and runs Tools/msdf-atlas-gen.exe (through
wine off Windows). Run it after changing the Chinese, Japanese, Korean or Thai tables: their
atlases only hold the characters those tables use. Full fonts live in FontSources/.

glossaries/<code>.md hold each language's chosen terms (ship names, weapons, run, tier...).
Translate new keys with them so terms stay consistent. Thai text marks the places a line may
wrap with zero-width spaces (U+200B) between words.
"""
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import OrderedDict

GAME = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
CONTENT = os.path.join(GAME, 'Content')
TABLES = os.path.join(CONTENT, 'Localization')
SKIP_DIRS = {'bin', 'obj', 'Tests', 'Simulation', 'tools', 'ThirdParty', 'Capture', 'ContentBuilder', 'Content'}
DATA_FILES = ['upgrades.json', 'upgrades_abilities.json', 'upgrades_meta.json']
DATA_FIELDS = ['name', 'tooltip', 'tooltip_extra']
# Tooltip values the code replaces before showing them.
DATA_SPECIAL = {'CollectionStrategy', 'No tooltip'}

LITERAL = r'"(?:[^"\\\n]|\\.)*"'
CALL = re.compile(r'\bLoc\.([TFN])\(\s*(' + LITERAL + ')')
PLURAL = re.compile(r'\bLoc\.P\(')
NOT_LITERAL = re.compile(r'\bLoc\.[TFNP]\(\s*[$@]"')
PLACEHOLDER = re.compile(r'\{(\d+)(?:[,:][^{}]*)?\}')
TAG = re.compile(r'\[(?:/?[a-zA-Z]+)(?:[ =][^\]]*)?\]')


def unescape(literal):
    body = literal[1:-1]
    escapes = {'n': '\n', 't': '\t', 'r': '\r', '0': '\0', '"': '"', "'": "'", '\\': '\\'}
    out, i = [], 0
    while i < len(body):
        c = body[i]
        if c == '\\' and i + 1 < len(body):
            n = body[i + 1]
            if n == 'u':
                out.append(chr(int(body[i + 2:i + 6], 16)))
                i += 6
                continue
            out.append(escapes.get(n, n))
            i += 2
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def in_comment(text, position):
    line_start = text.rfind('\n', 0, position) + 1
    return '//' in text[line_start:position]


def code_files():
    for root, dirs, files in os.walk(GAME):
        dirs[:] = sorted(d for d in dirs if d not in SKIP_DIRS and not d.startswith('.'))
        for name in sorted(files):
            if name.endswith('.cs') and '.Capture.' not in name:
                yield os.path.join(root, name)


def extract_code(keys, problems):
    for path in code_files():
        text = open(path, encoding='utf-8-sig').read()
        rel = os.path.relpath(path, GAME)
        for m in NOT_LITERAL.finditer(text):
            line = text.count('\n', 0, m.start()) + 1
            problems.append(f'{rel}:{line}: Loc call with an interpolated or verbatim string')
        for m in CALL.finditer(text):
            if not in_comment(text, m.start()):
                keys.setdefault(unescape(m.group(2)), rel)
        for m in PLURAL.finditer(text):
            if in_comment(text, m.start()):
                continue
            literals = re.compile(LITERAL).finditer(text, m.end())
            try:
                one, other = next(literals), next(literals)
            except StopIteration:
                continue
            keys.setdefault(unescape(one.group(0)) + '|' + unescape(other.group(0)), rel)


def extract_data(keys):
    for name in DATA_FILES:
        data = json.load(open(os.path.join(CONTENT, 'Data', name), encoding='utf-8'))
        items = data if isinstance(data, list) else next(v for v in data.values() if isinstance(v, list))
        for item in items:
            for field in DATA_FIELDS:
                value = item.get(field)
                if isinstance(value, str) and value.strip() and value not in DATA_SPECIAL:
                    keys.setdefault(value, 'Content/Data/' + name)


def extract_gum(keys):
    folder = os.path.join(CONTENT, 'GumProject', 'Screens')
    for name in sorted(os.listdir(folder)):
        if not name.endswith('.gusx'):
            continue
        root = ET.parse(os.path.join(folder, name)).getroot()
        for variable in root.iter('Variable'):
            var_name = variable.findtext('Name') or ''
            value = variable.findtext('Value')
            if var_name.endswith('.Text') and value and value.strip():
                keys.setdefault(value, 'Content/GumProject/Screens/' + name)


def extract():
    keys, problems = OrderedDict(), []
    extract_data(keys)
    extract_gum(keys)
    extract_code(keys, problems)
    os.makedirs(TABLES, exist_ok=True)
    write_table(os.path.join(TABLES, 'en.json'), OrderedDict((k, k) for k in keys))
    sources = OrderedDict()
    for key, source in keys.items():
        sources.setdefault(source, 0)
        sources[source] += 1
    print(f'{len(keys)} keys written to Content/Localization/en.json')
    for source, count in sources.items():
        print(f'  {count:4d}  {source}')
    for problem in problems:
        print('WARNING', problem)
    return keys


def write_table(path, table):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(table, f, ensure_ascii=False, indent=2)
        f.write('\n')


def languages():
    source = open(os.path.join(GAME, 'Localization', 'GameLanguage.cs'), encoding='utf-8').read()
    result = OrderedDict()
    for m in re.finditer(r'new\("([^"]+)", "[^"]*", "[^"]*", "[^"]*"(?:, "([^"]+)")?\)', source):
        result[m.group(1)] = m.group(2)
    return result


def plural_forms(code):
    if code in ('ru', 'pl'):
        return 3
    if code == 'cs':
        return 3
    if code in ('ja', 'ko', 'zh-Hans', 'th', 'vi', 'id'):
        return 1
    return 2


def check_entry(code, key, value):
    problems = []
    is_plural = '|' in key and not key.startswith('|')
    if is_plural:
        forms = value.split('|')
        if len(forms) != plural_forms(code):
            problems.append(f'needs {plural_forms(code)} plural forms, has {len(forms)}')
        key_forms = key.split('|')
        for form in forms:
            if set(PLACEHOLDER.findall(form)) != set(PLACEHOLDER.findall(key_forms[-1])):
                problems.append('placeholders differ in a plural form')
                break
        for form in forms:
            if sorted(TAG.findall(form)) != sorted(TAG.findall(key_forms[-1])):
                problems.append('[tags] differ in a plural form')
                break
    else:
        if sorted(PLACEHOLDER.findall(value)) != sorted(PLACEHOLDER.findall(key)):
            problems.append('placeholders differ')
        if sorted(TAG.findall(value)) != sorted(TAG.findall(key)):
            problems.append('[tags] differ')
    if value.count('\n') != key.count('\n') and not is_plural:
        problems.append('line breaks differ')
    return problems


def status():
    english = json.load(open(os.path.join(TABLES, 'en.json'), encoding='utf-8'))
    ok = True
    for code in languages():
        if code == 'en':
            continue
        path = os.path.join(TABLES, code + '.json')
        table = json.load(open(path, encoding='utf-8')) if os.path.exists(path) else {}
        missing = [k for k in english if not table.get(k)]
        stale = [k for k in table if k not in english]
        broken = []
        for key, value in table.items():
            if key in english and value:
                for problem in check_entry(code, key, value):
                    broken.append(f'{problem}: {key!r} -> {value!r}')
        ok &= not missing and not broken
        print(f'{code:8s} {len(english) - len(missing):5d}/{len(english)} translated, '
              f'{len(missing)} missing, {len(stale)} stale, {len(broken)} broken')
        for line in broken[:20]:
            print('   ', line)
    return ok


def sync():
    english = json.load(open(os.path.join(TABLES, 'en.json'), encoding='utf-8'))
    for code in languages():
        path = os.path.join(TABLES, code + '.json')
        if code == 'en' or not os.path.exists(path):
            continue
        table = json.load(open(path, encoding='utf-8'))
        write_table(path, OrderedDict((k, table[k]) for k in english if table.get(k)))
    print('Tables ordered like en.json; stale entries dropped.')


def fonts():
    try:
        from fontTools import subset
    except ImportError:
        sys.exit('fonts needs fontTools: pip install fonttools')
    english = json.load(open(os.path.join(TABLES, 'en.json'), encoding='utf-8'))
    base = set(chr(c) for c in range(0x20, 0x7F)) | set(chr(c) for c in range(0xA0, 0x100))
    base |= set(''.join(english))
    msdf = os.path.join(GAME, '..', '..', 'Tools', 'msdf-atlas-gen.exe')
    generated = os.path.join(CONTENT, 'Fonts', 'GeneratedFonts')
    os.makedirs(generated, exist_ok=True)
    for code, font in languages().items():
        if not font:
            continue
        path = os.path.join(TABLES, code + '.json')
        table = json.load(open(path, encoding='utf-8')) if os.path.exists(path) else {}
        names = set(''.join(m.group(1) for m in re.finditer(r'new\("[^"]+", "([^"]+)"',
            open(os.path.join(GAME, 'Localization', 'GameLanguage.cs'), encoding='utf-8').read())))
        characters = base | set(''.join(table.values())) | names
        characters = sorted(c for c in characters if c >= ' ' and c != '​')
        file_name = os.path.basename(font)
        source = os.path.join(GAME, 'FontSources', file_name)
        target = os.path.join(CONTENT, font)
        options = subset.Options()
        options.layout_features = ['*']
        options.notdef_outline = True
        options.name_IDs = ['*']
        loaded = subset.load_font(source, options)
        subsetter = subset.Subsetter(options)
        subsetter.populate(unicodes=[ord(c) for c in characters])
        subsetter.subset(loaded)
        subset.save_font(loaded, target, options)
        stem = os.path.splitext(file_name)[0]
        charset = os.path.join(generated, stem + '-charset.txt')
        with open(charset, 'w', encoding='utf-8') as f:
            f.write(', '.join(str(ord(c)) for c in characters))
        command = [msdf, '-font', target, '-imageout', os.path.join(generated, stem + '-atlas.png'),
                   '-type', 'mtsdf', '-charset', charset, '-size', '32', '-pxrange', '4',
                   '-json', os.path.join(generated, stem + '-layout.json'), '-yorigin', 'top']
        if os.name != 'nt':
            command = ['wine'] + command
        env = dict(os.environ, WINEDEBUG='-all')
        result = subprocess.run(command, env=env, capture_output=True, text=True)
        print(f'{code:8s} {file_name}: {len(characters)} characters, '
              f'{os.path.getsize(target) // 1024} KB font; ' + (result.stdout.strip().splitlines() or ['?'])[-1])


def main():
    command = sys.argv[1] if len(sys.argv) > 1 else 'status'
    if command == 'extract':
        extract()
    elif command == 'status':
        sys.exit(0 if status() else 1)
    elif command == 'sync':
        sync()
    elif command == 'fonts':
        fonts()
    else:
        sys.exit(__doc__)


if __name__ == '__main__':
    main()
