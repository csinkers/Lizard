cmake_minimum_required(VERSION 3.25)

set(VCPKG_USE_HOST_TOOLS ON)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON CACHE BOOL
    "Generate compile_commands.json for use with LSPs")

project(LizardTestCpp
  LANGUAGES C CXX
  VERSION 0.1.0
)

# Fix broken intellisense - https://developercommunity.visualstudio.com/t/IntelliSense-not-working-with-external-h/10892039
# Once the above issue is solved this can be removed.
unset(CMAKE_INCLUDE_SYSTEM_FLAG_CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_EXTENSIONS OFF)

include(CheckIncludeFile)
include(CheckIncludeFiles)
include(CheckCXXSourceCompiles)
include(CheckSymbolExists)

set(CMAKE_COLOR_DIAGNOSTICS ON)

# Windows
set(NOMINMAX ON)
set(project_name "${PROJECT_NAME}")
set(version      "${PROJECT_VERSION}")
set(CMAKE_INSTALL_RPATH "$ORIGIN/lib")
set(CMAKE_BUILD_WITH_INSTALL_RPATH ON)

# Add generated files to the sources
set(GENERATED_SOURCES
    ${CMAKE_SOURCE_DIR}/src/LizardComms.g.h
    ${CMAKE_SOURCE_DIR}/src/LizardComms.g.cpp
)

add_executable(LizardTestCpp
    ${GENERATED_SOURCES}
    src/CsConsole.h
    src/LizardFormat.cpp
    src/LizardFormat.h
    src/LizardProtocolTests.cpp
    src/LizardClient.cpp
    src/LizardServer.cpp
    src/Serdes.h
    src/main.cpp
    src/main.h
)

# Add a custom command to generate the C++ code from the F# project
# Run the code generator before building 'LizardTestCpp'
add_custom_command(
    OUTPUT ${GENERATED_SOURCES}
    COMMAND "dotnet" run --project ../LizardGenFs/LizardGenFs.fsproj -- ../LizardProtocol1.lproto -o src -cpp -ns LizardComms
    WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
    DEPENDS ${CMAKE_SOURCE_DIR}/../LizardProtocol1.lproto
    COMMENT "Generating C++ protocol files with LizardGenFs"
)

# add_dependencies(LizardTestCpp GenCpp)

function(print_all_variables)
  message(STATUS "CMake Variables:")
  get_cmake_property(_variableNames VARIABLES)
  list(SORT _variableNames)

  foreach (_variableName ${_variableNames})
    message(STATUS "${_variableName}=${${_variableName}}")
  endforeach()

endfunction()
# print_all_variables()

include_directories(
  src ${CMAKE_CURRENT_BINARY_DIR}/include
)

find_package(PkgConfig REQUIRED)
find_package(SDL2 CONFIG REQUIRED)
find_package(SDL2_net CONFIG REQUIRED)

target_include_directories(LizardTestCpp PRIVATE
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include/SDL2>
    $<INSTALL_INTERFACE:include/SDL2>
)

target_link_libraries(LizardTestCpp
    PRIVATE
    $<TARGET_NAME_IF_EXISTS:SDL2::SDL2main>
    $<IF:$<TARGET_EXISTS:SDL2::SDL2>,SDL2::SDL2,SDL2::SDL2-static>
)

target_link_libraries(LizardTestCpp PRIVATE
    $<IF:$<TARGET_EXISTS:SDL2_net::SDL2_net>,SDL2_net::SDL2_net,SDL2_net::SDL2_net-static>
)
