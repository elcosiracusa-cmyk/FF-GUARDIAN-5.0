rule FFGuardian_Yara_Test : informational ffguardian_test
{
    meta:
        author = "EL.CO by FFsoftware"
        description = "Test innocuo del motore YARA FFGuardian"
        category = "self-test"
        severity = "informational"
        date = "2026-08-04"

    strings:
        $test = "FFGUARDIAN_YARA_TEST_STRING"

    condition:
        $test
}
