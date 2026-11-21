Capsule-sized parts (912 px home base canvas), built by tools/build_capsule_set.py from the 4K
parts one folder up. Made to drop into a 1232x706 capsule without scaling: each image was
reduced once with a Lanczos filter, so edges stay clean. Scaling a part by up to about 2x
either way in Krita is fine; for anything bigger or much smaller, rebuild at another size:

    python3 tools/build_capsule_set.py RawAssets/Steam/parts <out_dir> <ship_px>

ship/<view>/   Every PNG in a view has the same canvas, so the layers line up when they
               share a position: ship.png (bare), armed_*.png (with turrets), turrets_*/ (one
               turret each) and engine_flames.png (the exhaust on its own).
effects/       Beams, bolts and flashes fire to the right. Line effects lie on the image's
               horizontal centre line (start and end x are listed below and in
               effects/anchors.json); flashes, flares and rings are centred. Rotate the layer
               about its start point to aim it. The game adds this light to the scene:
               Normal blend mode works, Addition gives the game's glow.
harvesters/    Sized as escorts next to the home base; harvesters_small/ is half that.
pickups/       Ore at 64, 96 and 128 px (64 is the in-game sprite size).

file  width x height  notes

effects/cannon/muzzle_flash.png                                  480 x 480   centred in the image
effects/cannon/round_1.png                                       223 x 52    flies right, along the centre line: the slug is at x=165
effects/cannon/round_2.png                                       226 x 52    flies right, along the centre line: the slug is at x=168
effects/cannon/round_3.png                                       225 x 52    flies right, along the centre line: the slug is at x=167
effects/cannon/round_4.png                                       228 x 52    flies right, along the centre line: the slug is at x=170
effects/cannon/round_critical_1.png                              404 x 90    flies right, along the centre line: the slug is at x=301
effects/cannon/round_critical_2.png                              408 x 90    flies right, along the centre line: the slug is at x=305
effects/engine/engine_flame_main_1.png                           289 x 338   exhaust points down
effects/engine/engine_flame_main_2.png                           291 x 344   exhaust points down
effects/engine/engine_flame_main_3.png                           289 x 346   exhaust points down
effects/engine/engine_flame_main_4.png                           287 x 344   exhaust points down
effects/engine/engine_flame_main_5.png                           282 x 338   exhaust points down
effects/engine/engine_flame_main_6.png                           281 x 333   exhaust points down
effects/engine/engine_flame_main_7.png                           283 x 330   exhaust points down
effects/engine/engine_flame_main_8.png                           287 x 333   exhaust points down
effects/engine/engine_flame_small_1.png                          161 x 189   exhaust points down
effects/engine/engine_flame_small_2.png                          161 x 192   exhaust points down
effects/engine/engine_flame_small_3.png                          161 x 194   exhaust points down
effects/engine/engine_flame_small_4.png                          159 x 192   exhaust points down
effects/engine/engine_flame_small_5.png                          157 x 189   exhaust points down
effects/engine/engine_flame_small_6.png                          156 x 186   exhaust points down
effects/engine/engine_flame_small_7.png                          158 x 185   exhaust points down
effects/engine/engine_flame_small_8.png                          159 x 186   exhaust points down
effects/harpoon/energy_lance.png                                 240 x 240   centred in the image
effects/harpoon/lightning_300_1.png                              525 x 314   fires right, along the centre line: starts at x=115, ends at x=415
effects/harpoon/lightning_300_2.png                              523 x 298   fires right, along the centre line: starts at x=114, ends at x=414
effects/harpoon/lightning_300_3.png                              522 x 270   fires right, along the centre line: starts at x=113, ends at x=413
effects/harpoon/lightning_600_1.png                              849 x 322   fires right, along the centre line: starts at x=130, ends at x=730
effects/harpoon/lightning_600_2.png                              856 x 316   fires right, along the centre line: starts at x=134, ends at x=734
effects/harpoon/lightning_600_3.png                              850 x 334   fires right, along the centre line: starts at x=131, ends at x=731
effects/harpoon/lightning_900_1.png                             1193 x 414   fires right, along the centre line: starts at x=153, ends at x=1053
effects/harpoon/lightning_900_2.png                             1198 x 466   fires right, along the centre line: starts at x=156, ends at x=1056
effects/harpoon/lightning_900_3.png                             1199 x 448   fires right, along the centre line: starts at x=156, ends at x=1056
effects/harpoon/ring.png                                         160 x 160   centred in the image
effects/harpoon/ring_on_surface.png                              160 x 160   centred in the image
effects/harpoon/ring_pulse.png                                   224 x 224   centred in the image
effects/laser/laser_beam_1000.png                               1337 x 410   fires right, along the centre line: starts at x=132, ends at x=1132
effects/laser/laser_beam_400.png                                 737 x 410   fires right, along the centre line: starts at x=132, ends at x=532
effects/laser/laser_beam_700.png                                1037 x 410   fires right, along the centre line: starts at x=132, ends at x=832
effects/laser/laser_body_only.png                                596 x 352   fires right, along the centre line: starts at x=-2, ends at x=598
effects/laser/laser_emitter_flare.png                            264 x 264   centred in the image
effects/laser/laser_impact_flare.png                             410 x 410   centred in the image
effects/laser/laser_surge_beam_700.png                          1145 x 542   fires right, along the centre line: starts at x=174, ends at x=874
effects/laser/laser_surge_body_only.png                          592 x 464   fires right, along the centre line: starts at x=-4, ends at x=596
effects/laser/laser_surge_emitter_flare.png                      348 x 348   centred in the image
effects/laser/laser_surge_impact_flare.png                       540 x 540   centred in the image
effects/laser/laser_thin_beam_1000.png                          1204 x 248   fires right, along the centre line: starts at x=80, ends at x=1080
effects/laser/laser_thin_beam_400.png                            604 x 248   fires right, along the centre line: starts at x=80, ends at x=480
effects/laser/laser_thin_beam_700.png                            904 x 248   fires right, along the centre line: starts at x=80, ends at x=780
effects/laser/laser_thin_body_only.png                           600 x 212   fires right, along the centre line: starts at x=0, ends at x=600
effects/laser/laser_thin_emitter_flare.png                       160 x 160   centred in the image
effects/laser/laser_thin_impact_flare.png                        246 x 246   centred in the image
effects/laser/planet_cracker_beam_700.png                       1336 x 778   fires right, along the centre line: starts at x=248, ends at x=948
effects/laser/planet_cracker_body_only.png                       584 x 670   fires right, along the centre line: starts at x=-8, ends at x=592
effects/laser/planet_cracker_emitter_flare.png                   496 x 496   centred in the image
effects/laser/planet_cracker_impact_flare.png                    774 x 774   centred in the image
effects/projectiles/harpoon_anchor_1.png                          51 x 77    
effects/projectiles/harpoon_anchor_2.png                          51 x 77    
effects/projectiles/harpoon_anchor_3.png                          51 x 77    
effects/projectiles/harpoon_anchor_4.png                          51 x 77    
effects/projectiles/rocket_main_1.png                             51 x 102   
effects/projectiles/rocket_main_2.png                             51 x 102   
effects/projectiles/rocket_main_3.png                             51 x 102   
effects/projectiles/rocket_main_4.png                             51 x 102   
effects/projectiles/rocket_mini_1.png                             38 x 77    
effects/projectiles/rocket_mini_2.png                             38 x 77    
effects/projectiles/rocket_mini_3.png                             38 x 77    
effects/projectiles/rocket_mini_4.png                             38 x 77    
effects/railgun/charge_star.png                                 1108 x 1108  centred in the image
effects/railgun/charge_vortex.png                                928 x 928   centred in the image
effects/railgun/ion_trail_500.png                                520 x 312   fires right, along the centre line: starts at x=7, ends at x=507
effects/railgun/ion_trail_800.png                                819 x 318   fires right, along the centre line: starts at x=7, ends at x=807
effects/railgun/muzzle_flash.png                                1214 x 1214  centred in the image
effects/railgun/muzzle_shock_ring.png                           1022 x 1022  centred in the image
effects/railgun/round_in_flight_500.png                          672 x 526   fires right, along the centre line: starts at x=11, ends at x=511
effects/railgun/round_in_flight_800.png                          972 x 526   fires right, along the centre line: starts at x=11, ends at x=811
effects/railgun/shot_impact_500.png                              633 x 468   fires right, along the centre line: starts at x=8, ends at x=508
effects/railgun/shot_impact_800.png                              933 x 468   fires right, along the centre line: starts at x=8, ends at x=808
harvesters/drifter/drifter_front34_left.png                      216 x 110   
harvesters/drifter/drifter_front34_right.png                     216 x 110   
harvesters/drifter/drifter_rear34_left.png                       216 x 113   
harvesters/drifter/drifter_rear34_right.png                      216 x 113   
harvesters/drifter/drifter_side_right.png                        216 x 129   
harvesters/drifter/drifter_top.png                               216 x 142   
harvesters/drone/drone_front34_left.png                          115 x 82    
harvesters/drone/drone_front34_right.png                         115 x 82    
harvesters/drone/drone_rear34_left.png                           115 x 67    
harvesters/drone/drone_rear34_right.png                          115 x 67    
harvesters/drone/drone_side_right.png                             96 x 115   
harvesters/drone/drone_top.png                                   115 x 51    
harvesters/prospector/prospector_front34_left.png                230 x 126   
harvesters/prospector/prospector_front34_right.png               230 x 126   
harvesters/prospector/prospector_rear34_left.png                 230 x 146   
harvesters/prospector/prospector_rear34_right.png                230 x 146   
harvesters/prospector/prospector_side_right.png                  230 x 93    
harvesters/prospector/prospector_top.png                         230 x 222   
harvesters/rimrunner/rimrunner_front34_left.png                  218 x 96    
harvesters/rimrunner/rimrunner_front34_right.png                 218 x 96    
harvesters/rimrunner/rimrunner_rear34_left.png                   218 x 139   
harvesters/rimrunner/rimrunner_rear34_right.png                  218 x 139   
harvesters/rimrunner/rimrunner_side_right.png                    218 x 190   
harvesters/rimrunner/rimrunner_top.png                           218 x 98    
harvesters/seeker/seeker_front34_left.png                        216 x 151   
harvesters/seeker/seeker_front34_right.png                       216 x 151   
harvesters/seeker/seeker_rear34_left.png                         216 x 112   
harvesters/seeker/seeker_rear34_right.png                        216 x 112   
harvesters/seeker/seeker_side_right.png                          216 x 143   
harvesters/seeker/seeker_top.png                                 216 x 137   
harvesters/trove_hunter/trove_hunter_front34_left.png            302 x 197   
harvesters/trove_hunter/trove_hunter_front34_right.png           302 x 197   
harvesters/trove_hunter/trove_hunter_rear34_left.png             302 x 168   
harvesters/trove_hunter/trove_hunter_rear34_right.png            302 x 168   
harvesters/trove_hunter/trove_hunter_side_right.png              302 x 128   
harvesters/trove_hunter/trove_hunter_top.png                     302 x 277   
harvesters_small/drifter/drifter_front34_left.png                111 x 57    
harvesters_small/drifter/drifter_front34_right.png               111 x 57    
harvesters_small/drifter/drifter_rear34_left.png                 111 x 58    
harvesters_small/drifter/drifter_rear34_right.png                111 x 58    
harvesters_small/drifter/drifter_side_right.png                  111 x 66    
harvesters_small/drifter/drifter_top.png                         111 x 73    
harvesters_small/drone/drone_front34_left.png                     59 x 42    
harvesters_small/drone/drone_front34_right.png                    59 x 42    
harvesters_small/drone/drone_rear34_left.png                      59 x 34    
harvesters_small/drone/drone_rear34_right.png                     59 x 34    
harvesters_small/drone/drone_side_right.png                       49 x 59    
harvesters_small/drone/drone_top.png                              59 x 26    
harvesters_small/prospector/prospector_front34_left.png          118 x 65    
harvesters_small/prospector/prospector_front34_right.png         118 x 65    
harvesters_small/prospector/prospector_rear34_left.png           118 x 75    
harvesters_small/prospector/prospector_rear34_right.png          118 x 75    
harvesters_small/prospector/prospector_side_right.png            118 x 48    
harvesters_small/prospector/prospector_top.png                   118 x 114   
harvesters_small/rimrunner/rimrunner_front34_left.png            112 x 49    
harvesters_small/rimrunner/rimrunner_front34_right.png           112 x 49    
harvesters_small/rimrunner/rimrunner_rear34_left.png             112 x 72    
harvesters_small/rimrunner/rimrunner_rear34_right.png            112 x 72    
harvesters_small/rimrunner/rimrunner_side_right.png              112 x 98    
harvesters_small/rimrunner/rimrunner_top.png                     112 x 50    
harvesters_small/seeker/seeker_front34_left.png                  111 x 78    
harvesters_small/seeker/seeker_front34_right.png                 111 x 78    
harvesters_small/seeker/seeker_rear34_left.png                   111 x 58    
harvesters_small/seeker/seeker_rear34_right.png                  111 x 58    
harvesters_small/seeker/seeker_side_right.png                    111 x 74    
harvesters_small/seeker/seeker_top.png                           111 x 70    
harvesters_small/trove_hunter/trove_hunter_front34_left.png      155 x 101   
harvesters_small/trove_hunter/trove_hunter_front34_right.png     155 x 101   
harvesters_small/trove_hunter/trove_hunter_rear34_left.png       155 x 86    
harvesters_small/trove_hunter/trove_hunter_rear34_right.png      155 x 86    
harvesters_small/trove_hunter/trove_hunter_side_right.png        155 x 65    
harvesters_small/trove_hunter/trove_hunter_top.png               155 x 142   
pickups/core_shard_128.png                                       128 x 128   
pickups/core_shard_192.png                                       192 x 192   
pickups/ore_glow_128/ore_1_blue.png                              128 x 128   
pickups/ore_glow_128/ore_1_darkblue.png                          128 x 128   
pickups/ore_glow_128/ore_1_gold.png                              128 x 128   
pickups/ore_glow_128/ore_1_green.png                             128 x 128   
pickups/ore_glow_128/ore_1_lilac.png                             128 x 128   
pickups/ore_glow_128/ore_1_purple.png                            128 x 128   
pickups/ore_glow_128/ore_1_red.png                               128 x 128   
pickups/ore_glow_128/ore_1_teal.png                              128 x 128   
pickups/ore_glow_128/ore_2_blue.png                              128 x 128   
pickups/ore_glow_128/ore_2_darkblue.png                          128 x 128   
pickups/ore_glow_128/ore_2_gold.png                              128 x 128   
pickups/ore_glow_128/ore_2_green.png                             128 x 128   
pickups/ore_glow_128/ore_2_lilac.png                             128 x 128   
pickups/ore_glow_128/ore_2_purple.png                            128 x 128   
pickups/ore_glow_128/ore_2_red.png                               128 x 128   
pickups/ore_glow_128/ore_2_teal.png                              128 x 128   
pickups/ore_glow_128/ore_3_blue.png                              128 x 128   
pickups/ore_glow_128/ore_3_darkblue.png                          128 x 128   
pickups/ore_glow_128/ore_3_gold.png                              128 x 128   
pickups/ore_glow_128/ore_3_green.png                             128 x 128   
pickups/ore_glow_128/ore_3_lilac.png                             128 x 128   
pickups/ore_glow_128/ore_3_purple.png                            128 x 128   
pickups/ore_glow_128/ore_3_red.png                               128 x 128   
pickups/ore_glow_128/ore_3_teal.png                              128 x 128   
pickups/ore_glow_128/ore_4_blue.png                              128 x 128   
pickups/ore_glow_128/ore_4_darkblue.png                          128 x 128   
pickups/ore_glow_128/ore_4_gold.png                              128 x 128   
pickups/ore_glow_128/ore_4_green.png                             128 x 128   
pickups/ore_glow_128/ore_4_lilac.png                             128 x 128   
pickups/ore_glow_128/ore_4_purple.png                            128 x 128   
pickups/ore_glow_128/ore_4_red.png                               128 x 128   
pickups/ore_glow_128/ore_4_teal.png                              128 x 128   
pickups/ore_glow_64/ore_1_blue.png                                64 x 64    
pickups/ore_glow_64/ore_1_darkblue.png                            64 x 64    
pickups/ore_glow_64/ore_1_gold.png                                64 x 64    
pickups/ore_glow_64/ore_1_green.png                               64 x 64    
pickups/ore_glow_64/ore_1_lilac.png                               64 x 64    
pickups/ore_glow_64/ore_1_purple.png                              64 x 64    
pickups/ore_glow_64/ore_1_red.png                                 64 x 64    
pickups/ore_glow_64/ore_1_teal.png                                64 x 64    
pickups/ore_glow_64/ore_2_blue.png                                64 x 64    
pickups/ore_glow_64/ore_2_darkblue.png                            64 x 64    
pickups/ore_glow_64/ore_2_gold.png                                64 x 64    
pickups/ore_glow_64/ore_2_green.png                               64 x 64    
pickups/ore_glow_64/ore_2_lilac.png                               64 x 64    
pickups/ore_glow_64/ore_2_purple.png                              64 x 64    
pickups/ore_glow_64/ore_2_red.png                                 64 x 64    
pickups/ore_glow_64/ore_2_teal.png                                64 x 64    
pickups/ore_glow_64/ore_3_blue.png                                64 x 64    
pickups/ore_glow_64/ore_3_darkblue.png                            64 x 64    
pickups/ore_glow_64/ore_3_gold.png                                64 x 64    
pickups/ore_glow_64/ore_3_green.png                               64 x 64    
pickups/ore_glow_64/ore_3_lilac.png                               64 x 64    
pickups/ore_glow_64/ore_3_purple.png                              64 x 64    
pickups/ore_glow_64/ore_3_red.png                                 64 x 64    
pickups/ore_glow_64/ore_3_teal.png                                64 x 64    
pickups/ore_glow_64/ore_4_blue.png                                64 x 64    
pickups/ore_glow_64/ore_4_darkblue.png                            64 x 64    
pickups/ore_glow_64/ore_4_gold.png                                64 x 64    
pickups/ore_glow_64/ore_4_green.png                               64 x 64    
pickups/ore_glow_64/ore_4_lilac.png                               64 x 64    
pickups/ore_glow_64/ore_4_purple.png                              64 x 64    
pickups/ore_glow_64/ore_4_red.png                                 64 x 64    
pickups/ore_glow_64/ore_4_teal.png                                64 x 64    
pickups/ore_glow_96/ore_1_blue.png                                96 x 96    
pickups/ore_glow_96/ore_1_darkblue.png                            96 x 96    
pickups/ore_glow_96/ore_1_gold.png                                96 x 96    
pickups/ore_glow_96/ore_1_green.png                               96 x 96    
pickups/ore_glow_96/ore_1_lilac.png                               96 x 96    
pickups/ore_glow_96/ore_1_purple.png                              96 x 96    
pickups/ore_glow_96/ore_1_red.png                                 96 x 96    
pickups/ore_glow_96/ore_1_teal.png                                96 x 96    
pickups/ore_glow_96/ore_2_blue.png                                96 x 96    
pickups/ore_glow_96/ore_2_darkblue.png                            96 x 96    
pickups/ore_glow_96/ore_2_gold.png                                96 x 96    
pickups/ore_glow_96/ore_2_green.png                               96 x 96    
pickups/ore_glow_96/ore_2_lilac.png                               96 x 96    
pickups/ore_glow_96/ore_2_purple.png                              96 x 96    
pickups/ore_glow_96/ore_2_red.png                                 96 x 96    
pickups/ore_glow_96/ore_2_teal.png                                96 x 96    
pickups/ore_glow_96/ore_3_blue.png                                96 x 96    
pickups/ore_glow_96/ore_3_darkblue.png                            96 x 96    
pickups/ore_glow_96/ore_3_gold.png                                96 x 96    
pickups/ore_glow_96/ore_3_green.png                               96 x 96    
pickups/ore_glow_96/ore_3_lilac.png                               96 x 96    
pickups/ore_glow_96/ore_3_purple.png                              96 x 96    
pickups/ore_glow_96/ore_3_red.png                                 96 x 96    
pickups/ore_glow_96/ore_3_teal.png                                96 x 96    
pickups/ore_glow_96/ore_4_blue.png                                96 x 96    
pickups/ore_glow_96/ore_4_darkblue.png                            96 x 96    
pickups/ore_glow_96/ore_4_gold.png                                96 x 96    
pickups/ore_glow_96/ore_4_green.png                               96 x 96    
pickups/ore_glow_96/ore_4_lilac.png                               96 x 96    
pickups/ore_glow_96/ore_4_purple.png                              96 x 96    
pickups/ore_glow_96/ore_4_red.png                                 96 x 96    
pickups/ore_glow_96/ore_4_teal.png                                96 x 96    
pickups/ore_plain_128/ore_1_blue.png                              91 x 96    same crystal, no glow
pickups/ore_plain_128/ore_1_darkblue.png                          91 x 96    same crystal, no glow
pickups/ore_plain_128/ore_1_gold.png                              91 x 96    same crystal, no glow
pickups/ore_plain_128/ore_1_green.png                             91 x 96    same crystal, no glow
pickups/ore_plain_128/ore_1_lilac.png                             91 x 96    same crystal, no glow
pickups/ore_plain_128/ore_1_purple.png                            91 x 96    same crystal, no glow
pickups/ore_plain_128/ore_1_red.png                               91 x 96    same crystal, no glow
pickups/ore_plain_128/ore_1_teal.png                              91 x 96    same crystal, no glow
pickups/ore_plain_128/ore_2_blue.png                              92 x 96    same crystal, no glow
pickups/ore_plain_128/ore_2_darkblue.png                          92 x 96    same crystal, no glow
pickups/ore_plain_128/ore_2_gold.png                              92 x 96    same crystal, no glow
pickups/ore_plain_128/ore_2_green.png                             92 x 96    same crystal, no glow
pickups/ore_plain_128/ore_2_lilac.png                             92 x 96    same crystal, no glow
pickups/ore_plain_128/ore_2_purple.png                            92 x 96    same crystal, no glow
pickups/ore_plain_128/ore_2_red.png                               92 x 96    same crystal, no glow
pickups/ore_plain_128/ore_2_teal.png                              92 x 96    same crystal, no glow
pickups/ore_plain_128/ore_3_blue.png                              86 x 96    same crystal, no glow
pickups/ore_plain_128/ore_3_darkblue.png                          86 x 96    same crystal, no glow
pickups/ore_plain_128/ore_3_gold.png                              86 x 96    same crystal, no glow
pickups/ore_plain_128/ore_3_green.png                             86 x 96    same crystal, no glow
pickups/ore_plain_128/ore_3_lilac.png                             86 x 96    same crystal, no glow
pickups/ore_plain_128/ore_3_purple.png                            86 x 96    same crystal, no glow
pickups/ore_plain_128/ore_3_red.png                               86 x 96    same crystal, no glow
pickups/ore_plain_128/ore_3_teal.png                              86 x 96    same crystal, no glow
pickups/ore_plain_128/ore_4_blue.png                              96 x 89    same crystal, no glow
pickups/ore_plain_128/ore_4_darkblue.png                          96 x 89    same crystal, no glow
pickups/ore_plain_128/ore_4_gold.png                              96 x 89    same crystal, no glow
pickups/ore_plain_128/ore_4_green.png                             96 x 89    same crystal, no glow
pickups/ore_plain_128/ore_4_lilac.png                             96 x 89    same crystal, no glow
pickups/ore_plain_128/ore_4_purple.png                            96 x 89    same crystal, no glow
pickups/ore_plain_128/ore_4_red.png                               96 x 89    same crystal, no glow
pickups/ore_plain_128/ore_4_teal.png                              96 x 89    same crystal, no glow
pickups/ore_plain_64/ore_1_blue.png                               46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_1_darkblue.png                           46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_1_gold.png                               46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_1_green.png                              46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_1_lilac.png                              46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_1_purple.png                             46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_1_red.png                                46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_1_teal.png                               46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_2_blue.png                               46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_2_darkblue.png                           46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_2_gold.png                               46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_2_green.png                              46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_2_lilac.png                              46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_2_purple.png                             46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_2_red.png                                46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_2_teal.png                               46 x 48    same crystal, no glow
pickups/ore_plain_64/ore_3_blue.png                               43 x 48    same crystal, no glow
pickups/ore_plain_64/ore_3_darkblue.png                           43 x 48    same crystal, no glow
pickups/ore_plain_64/ore_3_gold.png                               43 x 48    same crystal, no glow
pickups/ore_plain_64/ore_3_green.png                              43 x 48    same crystal, no glow
pickups/ore_plain_64/ore_3_lilac.png                              43 x 48    same crystal, no glow
pickups/ore_plain_64/ore_3_purple.png                             43 x 48    same crystal, no glow
pickups/ore_plain_64/ore_3_red.png                                43 x 48    same crystal, no glow
pickups/ore_plain_64/ore_3_teal.png                               43 x 48    same crystal, no glow
pickups/ore_plain_64/ore_4_blue.png                               48 x 45    same crystal, no glow
pickups/ore_plain_64/ore_4_darkblue.png                           48 x 45    same crystal, no glow
pickups/ore_plain_64/ore_4_gold.png                               48 x 45    same crystal, no glow
pickups/ore_plain_64/ore_4_green.png                              48 x 45    same crystal, no glow
pickups/ore_plain_64/ore_4_lilac.png                              48 x 45    same crystal, no glow
pickups/ore_plain_64/ore_4_purple.png                             48 x 45    same crystal, no glow
pickups/ore_plain_64/ore_4_red.png                                48 x 45    same crystal, no glow
pickups/ore_plain_64/ore_4_teal.png                               48 x 45    same crystal, no glow
pickups/ore_plain_96/ore_1_blue.png                               68 x 72    same crystal, no glow
pickups/ore_plain_96/ore_1_darkblue.png                           68 x 72    same crystal, no glow
pickups/ore_plain_96/ore_1_gold.png                               68 x 72    same crystal, no glow
pickups/ore_plain_96/ore_1_green.png                              68 x 72    same crystal, no glow
pickups/ore_plain_96/ore_1_lilac.png                              68 x 72    same crystal, no glow
pickups/ore_plain_96/ore_1_purple.png                             68 x 72    same crystal, no glow
pickups/ore_plain_96/ore_1_red.png                                68 x 72    same crystal, no glow
pickups/ore_plain_96/ore_1_teal.png                               68 x 72    same crystal, no glow
pickups/ore_plain_96/ore_2_blue.png                               69 x 72    same crystal, no glow
pickups/ore_plain_96/ore_2_darkblue.png                           69 x 72    same crystal, no glow
pickups/ore_plain_96/ore_2_gold.png                               69 x 72    same crystal, no glow
pickups/ore_plain_96/ore_2_green.png                              69 x 72    same crystal, no glow
pickups/ore_plain_96/ore_2_lilac.png                              69 x 72    same crystal, no glow
pickups/ore_plain_96/ore_2_purple.png                             69 x 72    same crystal, no glow
pickups/ore_plain_96/ore_2_red.png                                69 x 72    same crystal, no glow
pickups/ore_plain_96/ore_2_teal.png                               69 x 72    same crystal, no glow
pickups/ore_plain_96/ore_3_blue.png                               65 x 72    same crystal, no glow
pickups/ore_plain_96/ore_3_darkblue.png                           65 x 72    same crystal, no glow
pickups/ore_plain_96/ore_3_gold.png                               65 x 72    same crystal, no glow
pickups/ore_plain_96/ore_3_green.png                              65 x 72    same crystal, no glow
pickups/ore_plain_96/ore_3_lilac.png                              65 x 72    same crystal, no glow
pickups/ore_plain_96/ore_3_purple.png                             65 x 72    same crystal, no glow
pickups/ore_plain_96/ore_3_red.png                                65 x 72    same crystal, no glow
pickups/ore_plain_96/ore_3_teal.png                               65 x 72    same crystal, no glow
pickups/ore_plain_96/ore_4_blue.png                               72 x 67    same crystal, no glow
pickups/ore_plain_96/ore_4_darkblue.png                           72 x 67    same crystal, no glow
pickups/ore_plain_96/ore_4_gold.png                               72 x 67    same crystal, no glow
pickups/ore_plain_96/ore_4_green.png                              72 x 67    same crystal, no glow
pickups/ore_plain_96/ore_4_lilac.png                              72 x 67    same crystal, no glow
pickups/ore_plain_96/ore_4_purple.png                             72 x 67    same crystal, no glow
pickups/ore_plain_96/ore_4_red.png                                72 x 67    same crystal, no glow
pickups/ore_plain_96/ore_4_teal.png                               72 x 67    same crystal, no glow
pickups/prestige_orb_128.png                                     128 x 128   
pickups/prestige_orb_96.png                                       96 x 96    
planet/planet_250/crystal_planet_1.png                           250 x 254   
planet/planet_250/crystal_planet_2.png                           250 x 254   
planet/planet_250/crystal_planet_3.png                           250 x 253   
planet/planet_250/crystal_planet_4.png                           250 x 254   
planet/planet_250/crystal_planet_5.png                           250 x 254   
planet/planet_250/crystal_planet_6.png                           250 x 253   
planet/planet_330/crystal_planet_1.png                           330 x 335   
planet/planet_330/crystal_planet_2.png                           330 x 336   
planet/planet_330/crystal_planet_3.png                           330 x 334   
planet/planet_330/crystal_planet_4.png                           330 x 335   
planet/planet_330/crystal_planet_5.png                           330 x 336   
planet/planet_330/crystal_planet_6.png                           330 x 334   
planet/planet_450/crystal_planet_1.png                           450 x 456   
planet/planet_450/crystal_planet_2.png                           450 x 458   
planet/planet_450/crystal_planet_3.png                           450 x 455   
planet/planet_450/crystal_planet_4.png                           450 x 456   
planet/planet_450/crystal_planet_5.png                           450 x 458   
planet/planet_450/crystal_planet_6.png                           450 x 455   
ship/front34_left/armed_forward.png                              940 x 696   same canvas as the other layers of this view
ship/front34_left/armed_starboard.png                            940 x 696   same canvas as the other layers of this view
ship/front34_left/engine_flames.png                              940 x 696   engine exhaust, already cut where the hull hides it; goes above or below the ship layer
ship/front34_left/ship.png                                       940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_forward/cannon_left.png                940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_forward/cannon_right.png               940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_forward/harpoon.png                    940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_forward/laser.png                      940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_forward/railgun.png                    940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_forward/rockets.png                    940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_starboard/cannon_left.png              940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_starboard/cannon_right.png             940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_starboard/harpoon.png                  940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_starboard/laser.png                    940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_starboard/railgun.png                  940 x 696   same canvas as the other layers of this view
ship/front34_left/turrets_starboard/rockets.png                  940 x 696   same canvas as the other layers of this view
ship/front34_right/armed_forward.png                             912 x 552   same canvas as the other layers of this view
ship/front34_right/armed_starboard.png                           912 x 552   same canvas as the other layers of this view
ship/front34_right/engine_flames.png                             912 x 552   engine exhaust, already cut where the hull hides it; goes above or below the ship layer
ship/front34_right/ship.png                                      912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_forward/cannon_left.png               912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_forward/cannon_right.png              912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_forward/harpoon.png                   912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_forward/laser.png                     912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_forward/railgun.png                   912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_forward/rockets.png                   912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_starboard/cannon_left.png             912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_starboard/cannon_right.png            912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_starboard/harpoon.png                 912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_starboard/laser.png                   912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_starboard/railgun.png                 912 x 552   same canvas as the other layers of this view
ship/front34_right/turrets_starboard/rockets.png                 912 x 552   same canvas as the other layers of this view
ship/rear34_left/armed_forward.png                               912 x 571   same canvas as the other layers of this view
ship/rear34_left/armed_starboard.png                             912 x 571   same canvas as the other layers of this view
ship/rear34_left/engine_flames.png                               912 x 571   engine exhaust; goes above the ship layer
ship/rear34_left/ship.png                                        912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_forward/cannon_left.png                 912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_forward/cannon_right.png                912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_forward/harpoon.png                     912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_forward/laser.png                       912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_forward/railgun.png                     912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_forward/rockets.png                     912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_starboard/cannon_left.png               912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_starboard/cannon_right.png              912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_starboard/harpoon.png                   912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_starboard/laser.png                     912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_starboard/railgun.png                   912 x 571   same canvas as the other layers of this view
ship/rear34_left/turrets_starboard/rockets.png                   912 x 571   same canvas as the other layers of this view
ship/rear34_right/armed_forward.png                              941 x 636   same canvas as the other layers of this view
ship/rear34_right/armed_starboard.png                            941 x 636   same canvas as the other layers of this view
ship/rear34_right/engine_flames.png                              941 x 636   engine exhaust; goes above the ship layer
ship/rear34_right/ship.png                                       941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_forward/cannon_left.png                941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_forward/cannon_right.png               941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_forward/harpoon.png                    941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_forward/laser.png                      941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_forward/railgun.png                    941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_forward/rockets.png                    941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_starboard/cannon_left.png              941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_starboard/cannon_right.png             941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_starboard/harpoon.png                  941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_starboard/laser.png                    941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_starboard/railgun.png                  941 x 636   same canvas as the other layers of this view
ship/rear34_right/turrets_starboard/rockets.png                  941 x 636   same canvas as the other layers of this view
ship/side_left/armed_forward.png                                1063 x 395   same canvas as the other layers of this view
ship/side_left/armed_starboard.png                              1063 x 395   same canvas as the other layers of this view
ship/side_left/engine_flames.png                                1063 x 395   engine exhaust; goes above the ship layer
ship/side_left/ship.png                                         1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_forward/cannon_left.png                  1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_forward/cannon_right.png                 1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_forward/harpoon.png                      1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_forward/laser.png                        1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_forward/railgun.png                      1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_forward/rockets.png                      1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_starboard/cannon_left.png                1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_starboard/cannon_right.png               1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_starboard/harpoon.png                    1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_starboard/laser.png                      1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_starboard/railgun.png                    1063 x 395   same canvas as the other layers of this view
ship/side_left/turrets_starboard/rockets.png                    1063 x 395   same canvas as the other layers of this view
ship/side_right/armed_forward.png                               1064 x 295   same canvas as the other layers of this view
ship/side_right/armed_starboard.png                             1064 x 295   same canvas as the other layers of this view
ship/side_right/engine_flames.png                               1064 x 295   engine exhaust; goes above the ship layer
ship/side_right/ship.png                                        1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_forward/cannon_left.png                 1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_forward/cannon_right.png                1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_forward/harpoon.png                     1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_forward/laser.png                       1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_forward/railgun.png                     1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_forward/rockets.png                     1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_starboard/cannon_left.png               1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_starboard/cannon_right.png              1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_starboard/harpoon.png                   1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_starboard/laser.png                     1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_starboard/railgun.png                   1064 x 295   same canvas as the other layers of this view
ship/side_right/turrets_starboard/rockets.png                   1064 x 295   same canvas as the other layers of this view
ship/top/armed_forward.png                                       634 x 1116  same canvas as the other layers of this view
ship/top/armed_starboard.png                                     634 x 1116  same canvas as the other layers of this view
ship/top/engine_flames.png                                       634 x 1116  engine exhaust; goes above the ship layer
ship/top/ship.png                                                634 x 1116  same canvas as the other layers of this view
ship/top/turrets_forward/cannon_left.png                         634 x 1116  same canvas as the other layers of this view
ship/top/turrets_forward/cannon_right.png                        634 x 1116  same canvas as the other layers of this view
ship/top/turrets_forward/harpoon.png                             634 x 1116  same canvas as the other layers of this view
ship/top/turrets_forward/laser.png                               634 x 1116  same canvas as the other layers of this view
ship/top/turrets_forward/railgun.png                             634 x 1116  same canvas as the other layers of this view
ship/top/turrets_forward/rockets.png                             634 x 1116  same canvas as the other layers of this view
ship/top/turrets_starboard/cannon_left.png                       634 x 1116  same canvas as the other layers of this view
ship/top/turrets_starboard/cannon_right.png                      634 x 1116  same canvas as the other layers of this view
ship/top/turrets_starboard/harpoon.png                           634 x 1116  same canvas as the other layers of this view
ship/top/turrets_starboard/laser.png                             634 x 1116  same canvas as the other layers of this view
ship/top/turrets_starboard/railgun.png                           634 x 1116  same canvas as the other layers of this view
ship/top/turrets_starboard/rockets.png                           634 x 1116  same canvas as the other layers of this view
weapons/cannon/cannon_closeup_front34_left.png                   280 x 267   about twice its size on the ship
weapons/cannon/cannon_closeup_front34_right.png                  280 x 267   about twice its size on the ship
weapons/cannon/cannon_closeup_rear34_left.png                    237 x 280   about twice its size on the ship
weapons/cannon/cannon_closeup_rear34_right.png                   237 x 280   about twice its size on the ship
weapons/cannon/cannon_closeup_side_right.png                     280 x 176   about twice its size on the ship
weapons/cannon/top/cannon_base.png                               219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_000_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_022_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_045_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_067_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_090_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_112_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_135_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_157_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_180_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_202_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_225_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_247_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_270_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_292_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_315_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_full_337_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_000_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_022_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_045_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_067_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_090_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_112_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_135_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_157_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_180_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_202_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_225_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_247_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_270_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_292_5.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_315_0.png                         219 x 219   fits ship/top 1:1
weapons/cannon/top/cannon_head_337_5.png                         219 x 219   fits ship/top 1:1
weapons/harpoon/harpoon_closeup_front34_left.png                 280 x 261   about twice its size on the ship
weapons/harpoon/harpoon_closeup_front34_right.png                280 x 261   about twice its size on the ship
weapons/harpoon/harpoon_closeup_rear34_left.png                  228 x 280   about twice its size on the ship
weapons/harpoon/harpoon_closeup_rear34_right.png                 228 x 280   about twice its size on the ship
weapons/harpoon/harpoon_closeup_side_right.png                   280 x 219   about twice its size on the ship
weapons/harpoon/top/harpoon_base.png                             205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_000_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_022_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_045_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_067_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_090_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_112_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_135_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_157_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_180_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_202_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_225_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_247_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_270_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_292_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_315_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_full_337_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_000_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_022_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_045_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_067_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_090_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_112_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_135_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_157_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_180_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_202_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_225_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_247_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_270_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_292_5.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_315_0.png                       205 x 205   fits ship/top 1:1
weapons/harpoon/top/harpoon_head_337_5.png                       205 x 205   fits ship/top 1:1
weapons/laser/laser_closeup_front34_left.png                     251 x 280   about twice its size on the ship
weapons/laser/laser_closeup_front34_right.png                    251 x 280   about twice its size on the ship
weapons/laser/laser_closeup_rear34_left.png                      200 x 280   about twice its size on the ship
weapons/laser/laser_closeup_rear34_right.png                     199 x 280   about twice its size on the ship
weapons/laser/laser_closeup_side_right.png                       280 x 233   about twice its size on the ship
weapons/laser/top/laser_base.png                                 179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_000_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_022_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_045_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_067_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_090_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_112_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_135_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_157_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_180_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_202_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_225_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_247_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_270_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_292_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_315_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_full_337_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_000_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_022_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_045_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_067_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_090_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_112_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_135_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_157_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_180_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_202_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_225_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_247_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_270_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_292_5.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_315_0.png                           179 x 179   fits ship/top 1:1
weapons/laser/top/laser_head_337_5.png                           179 x 179   fits ship/top 1:1
weapons/railgun/railgun_closeup_front34_left.png                 374 x 270   about twice its size on the ship
weapons/railgun/railgun_closeup_front34_right.png                374 x 270   about twice its size on the ship
weapons/railgun/railgun_closeup_rear34_left.png                  374 x 356   about twice its size on the ship
weapons/railgun/railgun_closeup_rear34_right.png                 374 x 356   about twice its size on the ship
weapons/railgun/railgun_closeup_side_right.png                   374 x 205   about twice its size on the ship
weapons/railgun/top/railgun_base.png                             480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_000_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_022_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_045_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_067_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_090_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_112_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_135_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_157_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_180_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_202_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_225_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_247_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_270_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_292_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_315_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_full_337_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_000_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_022_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_045_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_067_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_090_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_112_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_135_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_157_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_180_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_202_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_225_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_247_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_270_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_292_5.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_315_0.png                       480 x 480   fits ship/top 1:1
weapons/railgun/top/railgun_head_337_5.png                       480 x 480   fits ship/top 1:1
weapons/rockets/rockets_closeup_front34_left.png                 194 x 280   about twice its size on the ship
weapons/rockets/rockets_closeup_front34_right.png                194 x 280   about twice its size on the ship
weapons/rockets/rockets_closeup_rear34_left.png                  198 x 280   about twice its size on the ship
weapons/rockets/rockets_closeup_rear34_right.png                 198 x 280   about twice its size on the ship
weapons/rockets/rockets_closeup_side_right.png                   214 x 280   about twice its size on the ship
weapons/rockets/top/rockets_base.png                             131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_000_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_022_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_045_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_067_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_090_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_112_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_135_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_157_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_180_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_202_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_225_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_247_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_270_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_292_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_315_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_full_337_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_000_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_022_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_045_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_067_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_090_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_112_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_135_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_157_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_180_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_202_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_225_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_247_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_270_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_292_5.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_315_0.png                       131 x 131   fits ship/top 1:1
weapons/rockets/top/rockets_head_337_5.png                       131 x 131   fits ship/top 1:1
