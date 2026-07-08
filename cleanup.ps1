$files = Get-ChildItem -Path "c:\Users\USER\Sistem_Berita_Acara\SistemBeritaAcara.Web" -Include *.razor,*.cs,*.css,*.js,*.md -Recurse | Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' -and $_.FullName -notmatch '\\wwwroot\\lib\\' }
$files += Get-ChildItem -Path "c:\Users\USER\Sistem_Berita_Acara\SistemBeritaAcara.Infrastructure" -Include *.cs,*.md -Recurse | Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' }
$files += Get-ChildItem -Path "c:\Users\USER\Sistem_Berita_Acara\SistemBeritaAcara.Core" -Include *.cs,*.md -Recurse | Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' }

foreach ($f in $files) {
    $content = Get-Content -Path $f.FullName -Raw -Encoding UTF8
    $original = $content

    # Fix Mojibake
    $content = $content -replace "â€”", "—" -replace "â”€", "─" -replace "â†", "←"

    # Replace Admin Gudang -> AdminBA
    if ($f.Name -match "\.md$") {
        $content = $content -replace "(?i)Admin Gudang Barang", "AdminBA"
        $content = $content -replace "(?i)Admin Gudang", "AdminBA"
    } else {
        $content = $content -replace "AdminGudangBarang", "AdminBA"
        $content = $content -replace "AdminGudang", "AdminBA"
        $content = $content -replace "adminGudangUsers", "adminBAUsers"
    }

    # OnlyOffice removals in app.css
    if ($f.Name -eq "app.css") {
        $content = $content -replace '(?m)^\.onlyoffice-container\s*\{[\s\S]*?\}', ''
        $content = $content -replace '(?m)^.*ONLYOFFICE:.*$', ''
    }
    
    # Remove OnlyOffice in README.md
    if ($f.Name -eq "README.md") {
        $content = $content -replace '(?m)^.*OnlyOffice.*$\r?\n', ''
    }

    # Remove Comments
    # Multi-line C# / CSS / JS
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '/\*[\s\S]*?\*/', '')
    # Razor comments
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '@\*[\s\S]*?\*@', '')
    # HTML comments
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '<!--[\s\S]*?-->', '')
    # Single line C# / JS (exclude URLs like http://, and exclude cases in string literals naively by ensuring space before // or start of line)
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)(^|\s)//.*$', '$1')

    if ($content -cne $original) {
        Set-Content -Path $f.FullName -Value $content -Encoding UTF8
    }
}
