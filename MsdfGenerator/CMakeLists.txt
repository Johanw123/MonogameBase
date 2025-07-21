cmake_minimum_required (VERSION 3.8)
project (msdfgenerator)

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED on)

add_subdirectory("./msdf-atlas-gen/")

add_executable(msdfgenerator DistanceFieldFontAtlas.cpp)

# target_link_libraries(game GL SDL2 SDL)
