namespace Nexo.IntegrationTests.Support;

/// <summary>Espera uma condição ficar verdadeira, para testes que dependem de processamento em segundo plano.</summary>
public static class Eventually
{
    public static async Task<bool> TrueAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return true;

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return await condition();
    }
}
