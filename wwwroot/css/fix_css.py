
import os

site_css_path = r"d:\Tasks\TodoList\TodoList\wwwroot\css\site.css"
btns_css_path = r"d:\Tasks\TodoList\TodoList\wwwroot\css\user_list_btns.css"

try:
    # Read site.css
    with open(site_css_path, 'r', encoding='utf-8', errors='ignore') as f:
        lines = f.readlines()

    # Find where corruption starts (looking for the spaced out comment)
    truncate_index = -1
    for i, line in enumerate(lines):
        if "/ *   - - -   U n i q u e" in line or ". b t n -" in line:
            truncate_index = i
            break
    
    if truncate_index != -1:
        print(f"Found corruption at line {truncate_index + 1}. Truncating...")
        clean_lines = lines[:truncate_index]
    else:
        print("No corruption marker found. Appending to end.")
        clean_lines = lines

    # Read the clean buttons CSS
    with open(btns_css_path, 'r', encoding='utf-8') as f:
        new_css = f.read()

    # Write back
    with open(site_css_path, 'w', encoding='utf-8') as f:
        f.writelines(clean_lines)
        f.write("\n" + new_css)
        
    print("Successfully fixed site.css")

except Exception as e:
    print(f"Error: {e}")
