_default:
    @just --list

generate-script:
    dotnet build SFD.Scripting.MapTranslationProject.csproj -t:GenerateScript

generate-translations:
    dotnet run --project SFD.Scripting.MapTranslationProject.Generator generate

validate-translations:
    dotnet run --project SFD.Scripting.MapTranslationProject.Generator validate

process-dump:
    #!/usr/bin/env python3
    import re
    from pathlib import Path

    dump_path = Path("translationdump.txt")
    maps_root = Path("db/maps")

    if not dump_path.exists():
        raise SystemExit("translationdump.txt not found in the repository root")

    # 1. Drop the storage-format header line.
    lines = dump_path.read_text(encoding="utf-8-sig").splitlines()[1:]

    # 2. Strip the leading `string|<storageKey>|` fields and unescape pipes.
    row_re = re.compile(r"^string\|[^|]*\|")
    rows = []
    for line in lines:
        match = row_re.match(line)
        if match:
            value = line[match.end():].replace("\\|", "|")
            if value.strip():
                rows.append(value)

    # 3. The first row is the map GUID; create its database folder.
    if not rows:
        raise SystemExit("dump contains no rows")

    guid = rows.pop(0)
    if not re.fullmatch(r"[0-9a-fA-F-]+", guid):
        raise SystemExit(f"unexpected GUID row: {guid!r}")

    target = maps_root / guid
    target.mkdir(parents=True, exist_ok=True)
    strings_path = target / "strings.tsv"

    # 4. Append rows not already present, deduplicating by placeholder key and
    #    by (original, kind) — maps repeat the same text on many objects.
    existing_keys = set()
    existing_pairs = set()
    if strings_path.exists():
        for line in strings_path.read_text(encoding="utf-8-sig").splitlines()[1:]:
            fields = line.split("\t")
            if len(fields) >= 3 and fields[1].strip():
                existing_keys.add(fields[1].strip())
                existing_pairs.add((fields[0], fields[2]))

    new_rows = []
    for row in rows:
        fields = row.split("\t")
        if len(fields) < 3:
            continue
        key = fields[1].strip()
        pair = (fields[0], fields[2])
        if key in existing_keys or pair in existing_pairs:
            continue
        existing_keys.add(key)
        existing_pairs.add(pair)
        new_rows.append(row)

    skipped = len(rows) - len(new_rows)

    with strings_path.open("a", encoding="utf-8") as stream:
        if strings_path.stat().st_size == 0:
            stream.write("original\tkey\tkind\n")
        for row in new_rows:
            stream.write(row + "\n")

    # 5. The dump has served its purpose.
    dump_path.unlink()

    message = f"{target}: appended {len(new_rows)} row(s)"
    if skipped:
        message += f", skipped {skipped} already present"
    print(message)

