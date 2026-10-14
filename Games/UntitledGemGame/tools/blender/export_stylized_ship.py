"""Run in Blender to export 128 stylized headings at each of five bank poses."""
from pathlib import Path
import importlib.util

spec = importlib.util.spec_from_file_location('btb_ship_looks', Path(__file__).with_name('ship_look_tests.py'))
looks = importlib.util.module_from_spec(spec)
spec.loader.exec_module(looks)
looks.setup()
try:
    looks.apply_style('stylized')
    looks.enable_acceleration()
    for index, bank in enumerate((-30, -15, 0, 15, 30)):
        looks.render_frames(bank_degrees=bank, bank_index=index)
finally:
    looks.finish()
