add_test([=[XXHTest.sums]=]  D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/xxhash/Debug/test_vrs_utils_xxhash.exe [==[--gtest_filter=XXHTest.sums]==] --gtest_also_run_disabled_tests)
set_tests_properties([=[XXHTest.sums]=]  PROPERTIES WORKING_DIRECTORY D:/repos/SeanWork/psi/Sources/Integrations/Aria/vrs/build/vrs/utils/xxhash SKIP_REGULAR_EXPRESSION [==[\[  SKIPPED \]]==])
set(  test_vrs_utils_xxhash_TESTS XXHTest.sums)
