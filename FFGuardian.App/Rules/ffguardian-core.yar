rule FFG_Test_EICAR : test
{
    meta:
        author = "FFGuardian by EL.CO"
        description = "EICAR antivirus test file"
        severity = "test"
    strings:
        $eicar = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*" ascii
    condition:
        $eicar
}

rule FFG_PowerShell_Encoded_Download : powershell suspicious
{
    meta:
        author = "FFGuardian by EL.CO"
        description = "Encoded PowerShell combined with network retrieval or execution"
        severity = "high"
    strings:
        $enc1 = "FromBase64String" ascii wide nocase
        $enc2 = "-EncodedCommand" ascii wide nocase
        $exec1 = "Invoke-Expression" ascii wide nocase
        $net1 = "Invoke-WebRequest" ascii wide nocase
        $net2 = "DownloadString" ascii wide nocase
        $net3 = "Start-BitsTransfer" ascii wide nocase
    condition:
        filesize < 5MB and (1 of ($enc*) and 1 of ($exec*, $net*))
}

rule FFG_Shadow_Copy_Destruction : ransomware suspicious
{
    meta:
        author = "FFGuardian by EL.CO"
        description = "Commands commonly used to destroy recovery copies"
        severity = "critical"
    strings:
        $vss1 = "vssadmin delete shadows" ascii wide nocase
        $vss2 = "wmic shadowcopy delete" ascii wide nocase
        $vss3 = "Delete Shadows /All" ascii wide nocase
        $bcd1 = "bcdedit /set {default} recoveryenabled no" ascii wide nocase
        $wb1 = "wbadmin delete catalog" ascii wide nocase
    condition:
        any of them
}

rule FFG_Ransom_Note_Generic : ransomware suspicious
{
    meta:
        author = "FFGuardian by EL.CO"
        description = "Generic ransom-note language with payment and recovery indicators"
        severity = "high"
    strings:
        $files1 = "your files have been encrypted" ascii wide nocase
        $files2 = "all your files are encrypted" ascii wide nocase
        $recover1 = "decrypt your files" ascii wide nocase
        $recover2 = "recover your files" ascii wide nocase
        $payment1 = "bitcoin" ascii wide nocase
        $payment2 = "payment" ascii wide nocase
        $contact1 = "tor browser" ascii wide nocase
    condition:
        filesize < 2MB and (1 of ($files*) or 1 of ($recover*)) and 1 of ($payment*, $contact*)
}
