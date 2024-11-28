#----------------------------------------------------------------
# Generated CMake target import file for configuration "Debug".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "vrs::vrs_helpers" for configuration "Debug"
set_property(TARGET vrs::vrs_helpers APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(vrs::vrs_helpers PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_DEBUG "CXX"
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/lib/vrs_helpers.lib"
  )

list(APPEND _cmake_import_check_targets vrs::vrs_helpers )
list(APPEND _cmake_import_check_files_for_vrs::vrs_helpers "${_IMPORT_PREFIX}/lib/vrs_helpers.lib" )

# Import target "vrs::vrs_os" for configuration "Debug"
set_property(TARGET vrs::vrs_os APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(vrs::vrs_os PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_DEBUG "CXX"
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/lib/vrs_os.lib"
  )

list(APPEND _cmake_import_check_targets vrs::vrs_os )
list(APPEND _cmake_import_check_files_for_vrs::vrs_os "${_IMPORT_PREFIX}/lib/vrs_os.lib" )

# Import target "vrs::vrs_logging" for configuration "Debug"
set_property(TARGET vrs::vrs_logging APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(vrs::vrs_logging PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_DEBUG "CXX"
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/lib/vrs_logging.lib"
  )

list(APPEND _cmake_import_check_targets vrs::vrs_logging )
list(APPEND _cmake_import_check_files_for_vrs::vrs_logging "${_IMPORT_PREFIX}/lib/vrs_logging.lib" )

# Import target "vrs::vrs_utils_converters" for configuration "Debug"
set_property(TARGET vrs::vrs_utils_converters APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(vrs::vrs_utils_converters PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_DEBUG "CXX"
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/lib/vrs_utils_converters.lib"
  )

list(APPEND _cmake_import_check_targets vrs::vrs_utils_converters )
list(APPEND _cmake_import_check_files_for_vrs::vrs_utils_converters "${_IMPORT_PREFIX}/lib/vrs_utils_converters.lib" )

# Import target "vrs::vrs_utils_xxhash" for configuration "Debug"
set_property(TARGET vrs::vrs_utils_xxhash APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(vrs::vrs_utils_xxhash PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_DEBUG "CXX"
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/lib/vrs_utils_xxhash.lib"
  )

list(APPEND _cmake_import_check_targets vrs::vrs_utils_xxhash )
list(APPEND _cmake_import_check_files_for_vrs::vrs_utils_xxhash "${_IMPORT_PREFIX}/lib/vrs_utils_xxhash.lib" )

# Import target "vrs::vrs_utils_cli" for configuration "Debug"
set_property(TARGET vrs::vrs_utils_cli APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(vrs::vrs_utils_cli PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_DEBUG "CXX"
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/lib/vrs_utils_cli.lib"
  )

list(APPEND _cmake_import_check_targets vrs::vrs_utils_cli )
list(APPEND _cmake_import_check_files_for_vrs::vrs_utils_cli "${_IMPORT_PREFIX}/lib/vrs_utils_cli.lib" )

# Import target "vrs::vrs_utils" for configuration "Debug"
set_property(TARGET vrs::vrs_utils APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(vrs::vrs_utils PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_DEBUG "CXX"
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/lib/vrs_utils.lib"
  )

list(APPEND _cmake_import_check_targets vrs::vrs_utils )
list(APPEND _cmake_import_check_files_for_vrs::vrs_utils "${_IMPORT_PREFIX}/lib/vrs_utils.lib" )

# Import target "vrs::vrslib" for configuration "Debug"
set_property(TARGET vrs::vrslib APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(vrs::vrslib PROPERTIES
  IMPORTED_LINK_INTERFACE_LANGUAGES_DEBUG "CXX"
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/lib/vrslib.lib"
  )

list(APPEND _cmake_import_check_targets vrs::vrslib )
list(APPEND _cmake_import_check_files_for_vrs::vrslib "${_IMPORT_PREFIX}/lib/vrslib.lib" )

# Import target "vrs::vrs" for configuration "Debug"
set_property(TARGET vrs::vrs APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(vrs::vrs PROPERTIES
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/bin/vrs.exe"
  )

list(APPEND _cmake_import_check_targets vrs::vrs )
list(APPEND _cmake_import_check_files_for_vrs::vrs "${_IMPORT_PREFIX}/bin/vrs.exe" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
