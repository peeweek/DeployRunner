#!/bin/bash

cd "$(dirname "$0")"
python -m venv .venv
.venv/bin/pip install --upgrade pip
.venv/bin/pip install -r ./requirements.txt
