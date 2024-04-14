import os
import fileinput
import sys
import re

os.chdir(os.path.dirname(os.path.realpath(__file__)))

old_name = input("project name: ")
name = input("project new name (empty for same): ")
if name == "": name = old_name
desc = input("project new description (empty for same): ")

if not os.path.isdir(old_name):
	input(f"No folder with name {old_name} --> exiting\n")
	exit()

if os.path.exists(name):
	input(f"This name ({old_name}) already exists --> exiting\n")
	exit()

os.rename(old_name, name)

for p in os.listdir(name):
	if "bin" in p or "obj" in p: continue

	p = f"{name}/{p}"

	if old_name in p:
		tmp = p.replace(old_name, name)
		os.rename(p, tmp)
		p = tmp

	for l in fileinput.input(p, True):
		if desc != "" and "<Description>" in l:
			l = re.sub(r"<Description>.*</Description>", f"<Description>{desc}</Description>", l)
		elif old_name in l:
			l = l.replace(old_name, name)
		sys.stdout.write(l)

if old_name == name: exit()
for p in os.listdir():
	if ".sln" not in p: continue

	for l in fileinput.input(p, True):
		if old_name in l:
			l = l.replace(old_name, name)
		sys.stdout.write(l)

	break