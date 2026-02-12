
import os

file_path = r'd:\Tasks\TodoList\TodoList\wwwroot\css\site.css'

with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

# We want to remove lines 7121 to 7154 (1-based)
# 0-based: remove indices 7120 to 7153 inclusive
# So we keep 0..7119 and 7154..end

new_lines = lines[:7120] + lines[7154:]

with open(file_path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print(f"Fixed. Original lines: {len(lines)}, New lines: {len(new_lines)}")
