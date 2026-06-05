import sys

filepath = "D:\Users\admin\Desktop\XYHMember\XYHMember\Views\Pharmacy\DispensingQuery.cshtml"

with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

with open(filepath, 'w', encoding='utf-8-sig') as f:
    f.write(content)

print("Fixed encoding: UTF-8 with BOM")
