import os
import glob
import argparse
from pathlib import Path
from fluent.syntax import ast, FluentParser, FluentSerializer

def move_and_rename_fluent_strings(base_dir, strings_to_move_file, source_filename, destination_filename, destination_base_dir, rename_keys, new_prefix):
    """
    Moves and optionally renames Fluent keys from a source file to a destination file across multiple language directories.
    
    Args:
        base_dir (str): The base directory containing all language folders.
        strings_to_move_file (str): Path to the text file with key names to move.
        source_filename (str): The name of the source FTL file to process.
        destination_filename (str): The name of the destination FTL file.
        destination_base_dir (str): Destination base directory containing language folders.
        rename_keys (bool): Whether to rename the keys during the move.
        new_prefix (str): New prefix for keys
    """
    parser = FluentParser()
    serializer = FluentSerializer()

    if not strings_to_move_file is None:
        if not os.path.exists(strings_to_move_file):
            print(f"Error: The strings list file '{strings_to_move_file}' does not exist.")
            return

    # Extract prefixes from filenames for renaming if the option is enabled
    source_prefix = None
    destination_prefix = None
    if rename_keys:
        source_prefix = os.path.splitext(source_filename)[0]
        destination_prefix = os.path.splitext(destination_filename)[0]
        if new_prefix is not None:
            destination_prefix = new_prefix

    if not strings_to_move_file is None:
        with open(strings_to_move_file, 'r', encoding='utf-8') as f:
            keys_to_move = {line.strip() for line in f if line.strip()}

        if not keys_to_move:
            print("No keys to move. Exiting.")
            return
    else:
        keys_to_move = "all"

    # Find all language subdirectories within the base directory
    language_dirs = glob.glob(os.path.join(base_dir, '*/'))

    if not language_dirs:
        print(f"No language directories found in '{base_dir}'. Exiting.")
        return

    for lang_dir in language_dirs:
        source_file = os.path.join(lang_dir, source_filename)
        lang_folder = Path(lang_dir).name
        
        if destination_base_dir is None:
            destination_base_dir = base_dir
        destination_final_dir = os.path.join(destination_base_dir, lang_folder)
        Path(destination_final_dir).mkdir(parents=True, exist_ok=True)
        destination_file = os.path.join(destination_final_dir, f"{destination_filename}")

        if not os.path.exists(source_file):
            print(f"Skipping {lang_dir}: '{source_filename}' not found.")
            continue

        print(f"\nProcessing '{source_file}'...")
        
        # Read and parse the source file
        with open(source_file, 'r', encoding='utf-8') as f:
            source_content = f.read()
        
        source_resource = parser.parse(source_content)
        
        moved_entries = []
        remaining_entries = []
        
        # Iterate through the AST to identify entries to move and optionally rename
        for entry in source_resource.body:
            if isinstance(entry, (ast.Message, ast.Term)):
                if keys_to_move == "all" or entry.id.name in keys_to_move:
                    if rename_keys:
                        old_id = entry.id.name
                        new_id_name = old_id.replace(source_prefix, destination_prefix, 1)
                        entry.id.name = new_id_name
                        print(f"Renamed '{old_id}' to '{new_id_name}'")
                    moved_entries.append(entry)
                else:
                    remaining_entries.append(entry)
            else:
                remaining_entries.append(entry)

        # Read and parse the destination file if it exists, otherwise create a new resource
        if os.path.exists(destination_file):
            with open(destination_file, 'r', encoding='utf-8') as f:
                destination_content = f.read()
            destination_resource = parser.parse(destination_content)
        else:
            destination_resource = ast.Resource(body=[])

        # Append the moved entries to the destination resource
        if moved_entries:
            # Check the last entry in the destination resource to add a blank line separator
            if destination_resource.body:
                last_entry = destination_resource.body[-1]
                if not isinstance(last_entry, ast.Junk):
                    destination_resource.body.append(ast.Junk(content="\n\n"))
            
            destination_resource.body.extend(moved_entries)
            serialized_destination = serializer.serialize(destination_resource)
            
            with open(destination_file, 'w', encoding='utf-8') as f:
                f.write(serialized_destination)
            print(f"Successfully moved {len(moved_entries)} keys to '{destination_file}'.")
        else:
            print("No keys found to move in this file.")

        # Update the source file by writing back the remaining entries
        remaining_resource = ast.Resource(body=remaining_entries)
        serialized_remaining = serializer.serialize(remaining_resource)
        with open(source_file, 'w', encoding='utf-8') as f:
            f.write(serialized_remaining)
        print("Updated the source file. Operation complete for this language.")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Move and optionally rename Fluent keys between files across language directories.")
    parser.add_argument("--base-dir", default=".", help="Base directory containing language folders.")
    parser.add_argument("--destination-base-dir", default=None, help="Destination base directory containing language folders.")
    parser.add_argument("--strings-to-move-file", default=None, help="Path to the file with keys to move.")
    parser.add_argument("--source-filename", default="common.ftl", help="Source FTL filename to process in each folder.")
    parser.add_argument("--destination-filename", default="new_component.ftl", help="Destination FTL filename where keys will be moved.")
    parser.add_argument("--rename", action="store_true", help="Enable renaming of keys based on file prefixes.")
    parser.add_argument("--new-prefix", help="New prefix.", default=None)
    
    args = parser.parse_args()
    
    move_and_rename_fluent_strings(
        base_dir=args.base_dir,
        strings_to_move_file=args.strings_to_move_file,
        source_filename=args.source_filename,
        destination_filename=args.destination_filename,
        destination_base_dir=args.destination_base_dir,
        rename_keys=args.new_prefix,
        new_prefix=args.new_prefix
    )