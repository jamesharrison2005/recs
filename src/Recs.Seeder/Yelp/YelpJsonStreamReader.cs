using System.Text.Json;

namespace Recs.Seeder.Yelp;

/// <summary>
/// Reads a line-delimited Yelp JSON file one line at a time.
/// <para>
/// The business and review files are several gigabytes, so the whole point of this type is that
/// it never holds more than a single line in memory. <see cref="File.ReadAllText"/> and
/// <c>JsonSerializer.Deserialize&lt;List&lt;T&gt;&gt;</c> are both ruled out for that reason.
/// </para>
/// </summary>
public static class YelpJsonStreamReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Streams <paramref name="path"/>, invoking <paramref name="handler"/> once per successfully
    /// parsed record. Lines that are blank or fail to parse are counted and skipped, so one bad
    /// line does not abort a multi-hour run.
    /// </summary>
    /// <returns>The number of lines that could not be parsed.</returns>
    public static async Task<int> ForEachAsync<T>(
        string path,
        Func<T, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Yelp data file not found: {path}", path);

        var malformed = 0;

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        using var reader = new StreamReader(stream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            T? record;
            try
            {
                record = JsonSerializer.Deserialize<T>(line, SerializerOptions);
            }
            catch (JsonException)
            {
                malformed++;
                continue;
            }

            if (record is null)
            {
                malformed++;
                continue;
            }

            await handler(record, cancellationToken);
        }

        return malformed;
    }
}
