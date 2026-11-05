using Skaar.NoApi.OpenApi;
using Skaar.NoApi.Services;

try
{
    if (args is [Generator.Command, .. var generatorArgs])
    {
        await Generator.Run(generatorArgs, Console.Out);
        return 0;
    }

    await using var server = Server.Create(args);
    await server.Run();
}
catch (Exception e)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine(e.Message);
    Console.ResetColor();
    return 1;
}

return 0;
