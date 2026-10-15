using System.IO.Compression;
using System.Security;
using System.Text;

namespace CloudConverter.Core;

/// <summary>시연용 샘플 파일 생성 (Word/Excel 에서 열리는 최소 구성의 문서)</summary>
public static class SampleFiles
{
    public static void CreateDocx(string path, string text)
    {
        WriteZip(path, new()
        {
            ["[Content_Types].xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                "</Types>",
            ["_rels/.rels"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                "</Relationships>",
            ["word/document.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" +
                string.Concat(text.Split('\n').Select(line => $"<w:p><w:r><w:t xml:space=\"preserve\">{SecurityElement.Escape(line)}</w:t></w:r></w:p>")) +
                "</w:body></w:document>",
        });
    }

    public static void CreateXlsx(string path, IReadOnlyList<string[]> rows)
    {
        var sheet = new StringBuilder();
        for (var r = 0; r < rows.Count; r++)
        {
            sheet.Append($"<row r=\"{r + 1}\">");
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = $"{(char)('A' + c)}{r + 1}";
                var value = rows[r][c];
                if (double.TryParse(value, out _))
                    sheet.Append($"<c r=\"{cell}\"><v>{value}</v></c>");
                else
                    sheet.Append($"<c r=\"{cell}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(value)}</t></is></c>");
            }
            sheet.Append("</row>");
        }

        WriteZip(path, new()
        {
            ["[Content_Types].xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "</Types>",
            ["_rels/.rels"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>",
            ["xl/workbook.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "</Relationships>",
            ["xl/worksheets/sheet1.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" +
                sheet + "</sheetData></worksheet>",
        });
    }

    /// <summary>ASCII 텍스트 한 줄짜리 PDF</summary>
    public static void CreatePdf(string path, string asciiText)
    {
        var escaped = asciiText.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var content = $"BT /F1 18 Tf 72 720 Td ({escaped}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            sb.Append($"{offset:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
    }

    /// <summary>암호화된 Office 문서처럼 보이는 파일 (OLE 복합 문서 헤더). "이미 암호화된 파일" 시연용</summary>
    public static void CreateEncryptedLookingOffice(string path)
    {
        var data = new byte[4096];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(data, 0);
        Encoding.ASCII.GetBytes("EncryptedPackage - CloudConverter demo sample").CopyTo(data, 512);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
    }

    private static bool IsEmpty(string directory) =>
        !Directory.Exists(directory) || !Directory.EnumerateFileSystemEntries(directory).Any();

    private static void WriteZip(string path, Dictionary<string, string> parts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, xml) in parts)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(xml);
        }
    }

    /// <summary>가상 클라우드 및 로컬 샘플 폴더에 시연용 파일 세트를 만든다. 이미 파일이 있으면 건드리지 않는다.</summary>
    public static void EnsureDemoContent(string cloudRoot, DemoSettings settings, string localSampleFolder)
    {
        var oneDrive = Path.Combine(cloudRoot, "OneDrive");
        if (IsEmpty(oneDrive))
        {
            CreateDocx(Path.Combine(oneDrive, "문서", "2025 사업계획서.docx"), "2025 사업계획서\n시연용 샘플 문서입니다.");
            CreateXlsx(Path.Combine(oneDrive, "문서", "분기별 매출현황.xlsx"), new[]
            {
                new[] { "분기", "매출(백만원)" }, new[] { "1Q", "1200" }, new[] { "2Q", "1350" }, new[] { "3Q", "1500" },
            });
            CreatePdf(Path.Combine(oneDrive, "문서", "Product Brochure.pdf"), "CloudConverter demo - Product Brochure");
            CreateDocx(Path.Combine(oneDrive, "회의록", "주간 회의록_1013.docx"), "주간 회의록\n- 안건 1\n- 안건 2");
            File.WriteAllText(Path.Combine(oneDrive, "회의록", "메모.txt"), "지원하지 않는 확장자 시연용");
        }

        foreach (var site in settings.SharePointSites)
        {
            var root = Path.Combine(cloudRoot, "SharePoint", site.Name);
            if (!IsEmpty(root))
                continue;
            CreateDocx(Path.Combine(root, "공유 문서", $"{site.Name} 업무 매뉴얼.docx"), $"{site.Name} 업무 매뉴얼");
            CreateXlsx(Path.Combine(root, "공유 문서", $"{site.Name} 자산 목록.xlsx"), new[]
            {
                new[] { "자산", "수량" }, new[] { "노트북", "25" }, new[] { "모니터", "40" },
            });
            CreatePdf(Path.Combine(root, "공유 문서", "Security Policy.pdf"), $"{site.Name} - Security Policy");
        }

        if (IsEmpty(localSampleFolder))
            CreateLocalSamples(localSampleFolder);
    }

    private static void CreateLocalSamples(string localSampleFolder)
    {
        CreateDocx(Path.Combine(localSampleFolder, "제안서_초안.docx"), "제안서 초안\n시연용 샘플 문서입니다.");
        CreateXlsx(Path.Combine(localSampleFolder, "견적서.xlsx"), new[]
        {
            new[] { "품목", "단가", "수량" }, new[] { "라이선스", "150000", "100" },
        });
        CreatePdf(Path.Combine(localSampleFolder, "Company Profile.pdf"), "CloudConverter demo - Company Profile");
        CreateEncryptedLookingOffice(Path.Combine(localSampleFolder, "이미 암호화된 보고서.docx"));
        File.WriteAllBytes(Path.Combine(localSampleFolder, "빈 파일.docx"), Array.Empty<byte>());
        CreateDocx(Path.Combine(localSampleFolder, "권한없음_fail.docx"), "강제 실패 시연용 (파일명에 _fail)");
        File.WriteAllText(Path.Combine(localSampleFolder, "readme.txt"), "지원하지 않는 확장자 시연용");
        CreateDocx(Path.Combine(localSampleFolder, "프로젝트 폴더", "요구사항 정의서.docx"), "요구사항 정의서");
        CreateXlsx(Path.Combine(localSampleFolder, "프로젝트 폴더", "일정표.xlsx"), new[]
        {
            new[] { "단계", "기간" }, new[] { "분석", "2주" }, new[] { "개발", "6주" },
        });
        CreateDocx(Path.Combine(localSampleFolder, "프로젝트 폴더", "산출물", "테스트 결과서.docx"), "테스트 결과서");
    }
}
