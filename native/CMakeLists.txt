cmake_minimum_required(VERSION 3.20)
project(cidn_wrapper C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

# Suppress CURL_EXTERN in vendored headers — we only need enums/typedefs/structs.
add_compile_definitions(CURL_STATICLIB)

# When building this DLL itself, export symbols.
add_compile_definitions(CIDN_BUILD_DLL)

set(SOURCES
    src/cidn_loader.c
    src/cidn_wrapper.c
)

add_library(cidn_wrapper SHARED ${SOURCES})

target_include_directories(cidn_wrapper PRIVATE
    include
    src
    third_party/curl/include
)

if(WIN32)
    # Nothing extra — MSVC links standard system libs automatically.
else()
    # dl for dlopen/dlsym, pthread for mutex.
    target_link_libraries(cidn_wrapper PRIVATE dl pthread)
    # Hide all symbols by default; only CIDN_API exports are visible.
    set_target_properties(cidn_wrapper PROPERTIES
        C_VISIBILITY_PRESET hidden
        VISIBILITY_INLINES_HIDDEN YES
    )
endif()

# Place the output in a predictable staging directory.
set_target_properties(cidn_wrapper PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/staging"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/staging"
)
