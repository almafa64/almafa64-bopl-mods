import os
import shutil
import fileinput
import sys

os.chdir(os.path.dirname(os.path.realpath(__file__)))

def delete_tree(path):
	try:
		shutil.rmtree(path)
	except FileNotFoundError:
		pass

old_name = "BoplBattleTemplate"
old_desc = "My first plugin"
name = input("project name: ")
if os.path.exists(name):
	input(f"The project {name} already exists \n")
	exit()
desc = input("project description: ")

delete_tree(f"{old_name}/obj")
delete_tree(f"{old_name}/bin")
shutil.copytree(old_name, name)

def replace_names(path: str):
	global name, old_name, old_desc, desc

	if path.endswith("thunderstore"):
		os.remove(f"{path}/{old_name}.zip")

	for p in os.listdir(path):
		p = f"{path}/{p}"

		if os.path.isdir(p): continue

		if old_name in p:
			tmp = p.replace(old_name, name)
			os.rename(p, tmp)
			p = tmp

		if not p.endswith((".cs", ".csproj", ".md", ".json")): continue

		if p.endswith("README.md"):
			with open(p, "w", encoding="utf8") as f:
				f.write(f"# {name}\n\n{desc}")
			continue

		for l in fileinput.input(p, True):
			if old_name in l:
				l = l.replace(old_name, name)
			if old_desc in l:
				l = l.replace(old_desc, desc)
			sys.stdout.write(l)

replace_names(name)
replace_names(f"{name}/thunderstore")