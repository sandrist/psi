if(EXISTS "D:/repos/winaria/vrs/build/vrs/helpers/test_vrs_helpers[1]_tests.cmake")
  include("D:/repos/winaria/vrs/build/vrs/helpers/test_vrs_helpers[1]_tests.cmake")
else()
  add_test(test_vrs_helpers_NOT_BUILT test_vrs_helpers_NOT_BUILT)
endif()
