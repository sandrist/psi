add_test([=[Raw10ToGrey10Converter.ContiguousImages]=]  D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/converters/Debug/test_vrs_utils_converters.exe [==[--gtest_filter=Raw10ToGrey10Converter.ContiguousImages]==] --gtest_also_run_disabled_tests)
set_tests_properties([=[Raw10ToGrey10Converter.ContiguousImages]=]  PROPERTIES WORKING_DIRECTORY D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/converters SKIP_REGULAR_EXPRESSION [==[\[  SKIPPED \]]==])
add_test([=[Raw10ToGrey10Converter.NonContiguousImages]=]  D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/converters/Debug/test_vrs_utils_converters.exe [==[--gtest_filter=Raw10ToGrey10Converter.NonContiguousImages]==] --gtest_also_run_disabled_tests)
set_tests_properties([=[Raw10ToGrey10Converter.NonContiguousImages]=]  PROPERTIES WORKING_DIRECTORY D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/converters SKIP_REGULAR_EXPRESSION [==[\[  SKIPPED \]]==])
add_test([=[Raw10ToGrey10Converter.InvalidInput]=]  D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/converters/Debug/test_vrs_utils_converters.exe [==[--gtest_filter=Raw10ToGrey10Converter.InvalidInput]==] --gtest_also_run_disabled_tests)
set_tests_properties([=[Raw10ToGrey10Converter.InvalidInput]=]  PROPERTIES WORKING_DIRECTORY D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/converters SKIP_REGULAR_EXPRESSION [==[\[  SKIPPED \]]==])
add_test([=[Raw10ToGrey10Converter.ContiguousImagesMultipleOf4]=]  D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/converters/Debug/test_vrs_utils_converters.exe [==[--gtest_filter=Raw10ToGrey10Converter.ContiguousImagesMultipleOf4]==] --gtest_also_run_disabled_tests)
set_tests_properties([=[Raw10ToGrey10Converter.ContiguousImagesMultipleOf4]=]  PROPERTIES WORKING_DIRECTORY D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/converters SKIP_REGULAR_EXPRESSION [==[\[  SKIPPED \]]==])
add_test([=[Raw10ToGrey10Converter.NonContiguousImagesMultipleOf4]=]  D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/converters/Debug/test_vrs_utils_converters.exe [==[--gtest_filter=Raw10ToGrey10Converter.NonContiguousImagesMultipleOf4]==] --gtest_also_run_disabled_tests)
set_tests_properties([=[Raw10ToGrey10Converter.NonContiguousImagesMultipleOf4]=]  PROPERTIES WORKING_DIRECTORY D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/converters SKIP_REGULAR_EXPRESSION [==[\[  SKIPPED \]]==])
set(  test_vrs_utils_converters_TESTS Raw10ToGrey10Converter.ContiguousImages Raw10ToGrey10Converter.NonContiguousImages Raw10ToGrey10Converter.InvalidInput Raw10ToGrey10Converter.ContiguousImagesMultipleOf4 Raw10ToGrey10Converter.NonContiguousImagesMultipleOf4)
