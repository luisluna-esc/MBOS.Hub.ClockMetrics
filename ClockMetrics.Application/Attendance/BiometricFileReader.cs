using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace ClockMetrics.Application.Attendance;

/// <summary>Una fila del archivo del biométrico: un empleado en un día, con todas sus marcas.</summary>
public record BiometricRow(int RowNumber, string DeviceCode, string Name, string Department, DateOnly Date, string Marks);

public record BiometricReadResult(List<BiometricRow> Rows, List<string> Errors);

// Lee el archivo que exporta el reloj (columnas AC-No, Nombre, Dpto., Fecha, Hora). El reloj lo
// escribe como .xls BIFF2 (formato de Excel de 1987, solo celdas de texto), que ni ClosedXML ni
// ExcelDataReader abren: ese formato se lee aquí a mano. Si alguien lo abre y lo guarda desde
// Excel como .xlsx, también se acepta.
public static class BiometricFileReader
{
    private static readonly string[] DateFormats = ["d/M/yyyy", "dd/MM/yyyy", "d/M/yy", "yyyy-MM-dd"];

    public static BiometricReadResult Read(byte[] content)
    {
        List<List<string>> table;
        try
        {
            if (content.Length >= 4 && content[0] == 'P' && content[1] == 'K')
                table = ReadXlsx(content);
            else if (content.Length >= 4 && content[0] == 0xD0 && content[1] == 0xCF)
                return new BiometricReadResult([], ["El archivo es un .xls de Excel 97-2003. Ábrelo en Excel y guárdalo como .xlsx, o sube el archivo tal como lo exporta el reloj."]);
            else
                table = ReadBiff(content);
        }
        catch (Exception)
        {
            return new BiometricReadResult([], ["No se pudo leer el archivo. Debe ser el .xls que exporta el biométrico o un .xlsx con las mismas columnas."]);
        }

        return ToRows(table);
    }

    private static BiometricReadResult ToRows(List<List<string>> table)
    {
        var errors = new List<string>();
        var headerIndex = table.FindIndex(row => row.Any(c => Normalize(c) == "acno") && row.Any(c => Normalize(c) == "hora"));
        if (headerIndex < 0)
            return new BiometricReadResult([], ["No se encontró la fila de títulos (AC-No, Nombre, Dpto., Fecha, Hora)."]);

        var header = table[headerIndex].Select(Normalize).ToList();
        int Column(params string[] names) => header.FindIndex(names.Contains);
        var codeColumn = Column("acno");
        var nameColumn = Column("nombre");
        var departmentColumn = Column("dpto", "departamento");
        var dateColumn = Column("fecha");
        var marksColumn = Column("hora", "horas");
        if (new[] { codeColumn, nameColumn, dateColumn, marksColumn }.Any(c => c < 0))
            return new BiometricReadResult([], ["Faltan columnas: el archivo debe tener AC-No, Nombre, Fecha y Hora."]);

        var rows = new List<BiometricRow>();
        for (var i = headerIndex + 1; i < table.Count; i++)
        {
            var row = table[i];
            string Cell(int column) => column >= 0 && column < row.Count ? row[column].Trim() : string.Empty;

            var code = Cell(codeColumn);
            if (code.Length == 0 && Cell(dateColumn).Length == 0)
                continue;

            if (!DateOnly.TryParseExact(Cell(dateColumn), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors.Add($"Fila {i + 1}: la fecha «{Cell(dateColumn)}» no es válida.");
                continue;
            }
            if (code.Length == 0)
            {
                errors.Add($"Fila {i + 1}: falta el AC-No.");
                continue;
            }

            var marks = AttendanceCalculator.ParseMarks(Cell(marksColumn));
            rows.Add(new BiometricRow(i + 1, code, Cell(nameColumn), Cell(departmentColumn), date,
                string.Join(' ', marks.Select(AttendanceCalculator.Format))));
        }

        if (rows.Count == 0 && errors.Count == 0)
            errors.Add("El archivo no tiene filas de marcaciones.");

        return new BiometricReadResult(rows, errors);
    }

    /// <summary>"AC-No" → "acno", "Dpto." → "dpto".</summary>
    private static string Normalize(string value) =>
        new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static List<List<string>> ReadXlsx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var table = new List<List<string>>();
        foreach (var row in sheet.RowsUsed())
        {
            var values = new List<string>();
            var lastColumn = row.LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (var c = 1; c <= lastColumn; c++)
            {
                var cell = row.Cell(c);
                values.Add(cell.DataType == XLDataType.DateTime
                    ? cell.GetDateTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                    : cell.GetFormattedString());
            }
            // La tabla se indexa por número de fila real, para que los errores digan la fila de Excel.
            while (table.Count < row.RowNumber() - 1) table.Add([]);
            table.Add(values);
        }
        return table;
    }

    /// <summary>
    /// Lector mínimo de BIFF2-BIFF5 sin contenedor OLE: recorre los registros (tipo, largo, datos)
    /// y toma las celdas de texto (LABEL 0x0004 en BIFF2, 0x0204 en BIFF3-5) y de número.
    /// Los textos vienen en Windows-1252 (el "ñ" de Siñani es 0xF1).
    /// </summary>
    private static List<List<string>> ReadBiff(byte[] content)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(1252);
        var cells = new Dictionary<(int Row, int Column), string>();

        var position = 0;
        var first = true;
        while (position + 4 <= content.Length)
        {
            var type = BitConverter.ToUInt16(content, position);
            var length = BitConverter.ToUInt16(content, position + 2);
            var dataStart = position + 4;
            if (dataStart + length > content.Length)
                break;

            if (first)
            {
                // Todo BIFF empieza con un registro BOF (0x0009, 0x0209, 0x0409 o 0x0809).
                if ((type & 0xFF) != 0x09)
                    throw new InvalidDataException("No es un archivo BIFF.");
                first = false;
            }

            switch (type)
            {
                case 0x0004 when length >= 8: // LABEL BIFF2: fila, columna, 3 bytes de formato, largo (1 byte), texto
                {
                    var textLength = content[dataStart + 7];
                    cells[(Row(dataStart), Column(dataStart))] = encoding.GetString(content, dataStart + 8, Math.Min(textLength, length - 8));
                    break;
                }
                case 0x0204 when length >= 8: // LABEL BIFF3-5: fila, columna, formato (2), largo (2), texto
                {
                    var textLength = BitConverter.ToUInt16(content, dataStart + 6);
                    cells[(Row(dataStart), Column(dataStart))] = encoding.GetString(content, dataStart + 8, Math.Min(textLength, length - 8));
                    break;
                }
                case 0x0002 when length >= 9: // INTEGER BIFF2
                    cells[(Row(dataStart), Column(dataStart))] = BitConverter.ToUInt16(content, dataStart + 7).ToString(CultureInfo.InvariantCulture);
                    break;
                case 0x0003 when length >= 15: // NUMBER BIFF2
                    cells[(Row(dataStart), Column(dataStart))] = BitConverter.ToDouble(content, dataStart + 7).ToString(CultureInfo.InvariantCulture);
                    break;
                case 0x0203 when length >= 14: // NUMBER BIFF3-5
                    cells[(Row(dataStart), Column(dataStart))] = BitConverter.ToDouble(content, dataStart + 6).ToString(CultureInfo.InvariantCulture);
                    break;
            }

            if (type == 0x000A) // EOF
                break;
            position = dataStart + length;
        }

        if (first)
            throw new InvalidDataException("Archivo vacío.");

        var table = new List<List<string>>();
        if (cells.Count == 0)
            return table;

        var maxRow = cells.Keys.Max(k => k.Row);
        var maxColumn = cells.Keys.Max(k => k.Column);
        for (var r = 0; r <= maxRow; r++)
        {
            var row = new List<string>(maxColumn + 1);
            for (var c = 0; c <= maxColumn; c++)
                row.Add(cells.TryGetValue((r, c), out var value) ? value : string.Empty);
            table.Add(row);
        }
        return table;

        int Row(int offset) => BitConverter.ToUInt16(content, offset);
        int Column(int offset) => BitConverter.ToUInt16(content, offset + 2);
    }
}
