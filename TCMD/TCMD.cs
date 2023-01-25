using System;
using System.Net;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;

namespace TCMD;

internal class TDynamicMethod
{
    public string Name { get; private set; }
    public Delegate Action { get; private set; }
    public TDMParameter[]? Parameters { get; private set; }

    public TDynamicMethod(string name, Delegate action)
    {
        Name = name;
        Action = action;
        Parameters = Action.Method.GetParameters().Select(x => new TDMParameter(x.Name ?? x.Position.ToString(), x.ParameterType, x.DefaultValue)).ToArray();
    }

    public Task? Invoke(TArgument[]? args)
    {
        object?[]? parameters = new object?[Parameters?.Length ?? 0];
        for (int i = 0; i < Parameters?.Length; i++)
        {
            var parameter = Parameters[i];
            var arg = args?.FirstOrDefault(x => x.Name == parameter.Name);
            if (arg == null)
            {
                if (parameter.DefaultValue == null)
                {
                    Command.Error($"Missing argument {parameter.Name}");
                    return Task.CompletedTask;
                }
                try
                {
                    parameters[i] = Convert.ChangeType(parameter.DefaultValue, parameter.Type);
                } catch
                {
                    Command.Error($"Failed to convert argument -{parameter.Name} with value '{parameter.DefaultValue}' to {parameter.Type.Name}");
                    return Task.CompletedTask;
                }
            }
            else if(arg.Value != null)
            {
                try
                {
                    parameters[i] = Convert.ChangeType(arg.Value, parameter.Type);
                }
                catch 
                {
                    Command.Error($"Failed to convert argument -{parameter.Name} with value '{arg.Value}' to {parameter.Type.Name}");
                    return Task.CompletedTask;
                }
            }
        }
        if (Action.Method.ReturnType == typeof(Task))
            return (Task?)Action.DynamicInvoke(parameters);
        else return Task.Run(() => Action.DynamicInvoke(parameters));
    }

    public class TDMParameter
    {
        public string Name { get; private set; }
        public Type Type { get; private set; }
        public bool HasDefaultValue { get; private set; }
        public object? DefaultValue { get; private set; }

        public TDMParameter(string name, Type type, object? defaultValue = null)
        {
            Name = name;
            Type = type;
            HasDefaultValue = defaultValue != null;
            DefaultValue = defaultValue;
        }
    }
}

public class TArgument
{
    public string Name { get; private set; }
    public object? Value { get; private set; }

    public TArgument(string name, object? value = null)
    {
        Name = name;
        Value = value;
    }
}

public static class Command
{
    public static bool DefaultToHelp { get; set; } = true;
    private static List<TDynamicMethod> AdditionalMethods { get; set; } = new List<TDynamicMethod>();

    public static async Task ParseArguments()
    {
        try
        {
            await Parse(Environment.GetCommandLineArgs().Skip(1).ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    public static void Add(string name, Delegate action)
    {
        AdditionalMethods.Add(new TDynamicMethod(name, action));
    }

    public static void Help()
    {
        Assembly? assembly = Assembly.GetEntryAssembly();
        AssemblyName? assemblyName = assembly?.GetName();

        if (assembly != null && assemblyName != null)
        {
            Console.WriteLine("****************************");
            Console.WriteLine($"Assembly Name: {assemblyName.Name}");
            var assemblyCompanyAttribute = assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
            if (assemblyCompanyAttribute.Length > 0)
            {
                var companyAttribute = (AssemblyCompanyAttribute)assemblyCompanyAttribute[0];
                Console.WriteLine($"Author: {companyAttribute.Company}");
            }
            Console.WriteLine($"Version: {assemblyName.Version}");
        }
        Console.WriteLine("*********** HELP ***********");
        Console.WriteLine();

        var methods = GetMethods();
        if (methods.Count > 0)
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine();

            foreach (var m in methods)
            {
                Console.WriteLine($" {m.Name}:");
                if (m.Parameters?.Length > 0)
                {
                    Console.WriteLine("  Parameters:");
                    foreach (var p in m.Parameters)
                    {
                        Console.WriteLine($"    -{p.Name} ({p.Type.Name}){(p.HasDefaultValue ? $" (default: {p.DefaultValue})" : "")}");
                    }
                }
                else
                {
                    Console.WriteLine("  This command does not take any parameters.");
                }
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine("No commands are available.");
        }
    }

    private static List<TDynamicMethod> GetMethods()
    {
        var methodsDefined = Assembly.GetEntryAssembly()?
            .GetTypes()
            .SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttributes(typeof(CMD), false).Length > 0)
            .ToList() ?? new List<MethodInfo>();
        var methods = methodsDefined.Select(x => {
            var param = x.GetParameters().Select(y => y.ParameterType).ToArray();
            var dt = GenericActionTypeParameters(param.Length).MakeGenericType(param);
            Delegate d = x.CreateDelegate(dt); 
            return new TDynamicMethod(x.Name, d);
        }).ToList();
        methods.AddRange(AdditionalMethods);
        return methods;
    }

    internal static void Error(string msg)
    {
        ConsoleColor prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.Error.WriteLine(msg);
        Console.ForegroundColor = prev;
    }

    private static Type GenericActionTypeParameters(int c)
    {
        // this is required due to a bug in .NET
        return c switch
        {
            0 => typeof(Action),
            1 => typeof(Action<>),
            2 => typeof(Action<,>),
            3 => typeof(Action<,,>),
            4 => typeof(Action<,,,>),
            5 => typeof(Action<,,,,>),
            6 => typeof(Action<,,,,,>),
            7 => typeof(Action<,,,,,,>),
            8 => typeof(Action<,,,,,,,>),
            9 => typeof(Action<,,,,,,,,>),
            10 => typeof(Action<,,,,,,,,>),
            11 => typeof(Action<,,,,,,,,,>),
            12 => typeof(Action<,,,,,,,,,,>),
            13 => typeof(Action<,,,,,,,,,,,>),
            14 => typeof(Action<,,,,,,,,,,,,>),
            15 => typeof(Action<,,,,,,,,,,,,,>),
            16 => typeof(Action<,,,,,,,,,,,,,,>),
            17 => typeof(Action<,,,,,,,,,,,,,,,>),
            _ => typeof(Action),
        };
    }
    
    public static async Task Parse(params string[] args)
    {
        if (args.Length == 0 && DefaultToHelp) {
            Help();
            return;
        } else if(args.Length == 0 && !DefaultToHelp)
        {
            Command.Error("No parameters presented");
            return;
        }
        
        TDynamicMethod? func = GetMethods().FirstOrDefault(x => x.Name.ToLower() == args[0].ToLower()) ?? null;
        if (func == null)
        {
            Command.Error($"Command {args[0]} not found.");
            return;
        }

        TArgument[]? newArgs = GetArguments(StringifyArguments(args.Skip(1)));
        Task? invocation = func.Invoke(newArgs);
        if (invocation != null)
            await invocation;
    }

    private static string StringifyArguments(IEnumerable<string> args)
    {
        var sb = new StringBuilder();
        foreach (var arg in args)
        {
            if(arg.Contains(' '))
            {
                sb.Append($"\"{arg}\"");
            } else
            {
                sb.Append(arg);
            }
            sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    private static TArgument[] GetArguments(string input)
    {
        string pattern = "(?<name>-\\w+)(?:\\s+(?!-)(?<value>(?:\"[^\\\"]*\"|\\S+)))?";
        var arguments = Regex.Matches(input, pattern)
            .Cast<Match>()
            .Select(m => new TArgument(m.Groups["name"].Value[1..], m.Groups["value"].Success ? m.Groups["value"].Value.Trim('"') : (object)true)).ToArray();
        return arguments;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class CMD : Attribute
{
    public string Alt { get; set; }

    public CMD(string Alt = "")
    {
        this.Alt = Alt;
    }
}
