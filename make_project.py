import os
import shutil
import fileinput
import sys

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

for p in os.listdir(name):
	p = f"{name}/{p}"

	if old_name in p:
		tmp = p.replace(old_name, name)
		os.rename(p, tmp)
		p = tmp
	
	proj = p.endswith(".csporj")

	for l in fileinput.input(p, True):
		if old_name in l:
			l = l.replace(old_name, name)
		if old_desc in l:
			l = l.replace(old_desc, desc)
		sys.stdout.write(l)