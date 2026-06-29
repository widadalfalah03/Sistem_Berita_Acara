$word = New-Object -ComObject Word.Application
$word.Visible = $false
try {
    $templates = @("$PWD\wwwroot\files\templates\BA_Template_Peminjaman.docx", "$PWD\wwwroot\files\templates\BA_Template_Alokasi.docx")
    
    foreach ($docPath in $templates) {
        $doc = $word.Documents.Open($docPath)
        $targetTable = $null
        foreach ($table in $doc.Tables) {
            $text = $table.Range.Text
            if ($text -match 'NOMOR SERIAL' -and $text -match 'KETERANGAN') {
                $targetTable = $table
                break
            }
        }
        
        if ($targetTable) {
            # Loop through all cells and center them horizontally and vertically
            foreach ($cell in $targetTable.Range.Cells) {
                $cell.VerticalAlignment = 1 # wdCellAlignVerticalCenter
                $cell.Range.ParagraphFormat.Alignment = 1 # wdAlignParagraphCenter
            }
            
            # The last row has "Catatan:", it should be aligned left and top maybe?
            # Wait, the last row in Peminjaman is the "Catatan" row.
            # Let's find cells that contain "Catatan" and left align them
            foreach ($cell in $targetTable.Range.Cells) {
                if ($cell.Range.Text -match 'Catatan') {
                    $cell.VerticalAlignment = 0 # wdCellAlignVerticalTop
                    $cell.Range.ParagraphFormat.Alignment = 0 # wdAlignParagraphLeft
                }
            }
            
            Write-Host "Updated table in $($doc.Name)"
            $doc.Save()
        }
        $doc.Close([ref]0)
    }
} catch {
    Write-Host "Error: $_"
} finally {
    $word.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
}
