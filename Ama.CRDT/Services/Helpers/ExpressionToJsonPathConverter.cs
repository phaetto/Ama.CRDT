namespace Ama.CRDT.Services.Helpers;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;

internal static class ExpressionToJsonPathConverter
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Convert<T, TProperty>(Expression<Func<T, TProperty>> expression)
    {
        var pathSegments = new Stack<string>();
        var currentExpression = expression.Body;

        while (currentExpression is not null and not ParameterExpression)
        {
            currentExpression = UnpackExpression(currentExpression, pathSegments);
        }

        if (pathSegments.Count == 0)
        {
            return "$";
        }

        var sb = new StringBuilder("$");
        foreach (var segment in pathSegments)
        {
            if (segment.StartsWith("["))
            {
                sb.Append(segment);
            }
            else
            {
                sb.Append('.').Append(segment);
            }
        }
        return sb.ToString();
    }

    private static Expression? UnpackExpression(Expression currentExpression, Stack<string> pathSegments) => currentExpression.NodeType switch
    {
        ExpressionType.MemberAccess => UnpackMemberAccess((MemberExpression)currentExpression, pathSegments),
        ExpressionType.Call => UnpackMethodCall((MethodCallExpression)currentExpression, pathSegments),
        ExpressionType.ArrayIndex => UnpackArrayIndex((BinaryExpression)currentExpression, pathSegments),
        ExpressionType.Convert => ((UnaryExpression)currentExpression).Operand,
        _ => throw new NotSupportedException($"Expression type '{currentExpression.NodeType}' is not supported in path expressions.")
    };

    private static Expression UnpackMemberAccess(MemberExpression memberExpression, Stack<string> pathSegments)
    {
        var propertyName = SerializerOptions.PropertyNamingPolicy?.ConvertName(memberExpression.Member.Name) ?? memberExpression.Member.Name;
        pathSegments.Push(propertyName);
        return memberExpression.Expression!;
    }

    private static Expression UnpackMethodCall(MethodCallExpression methodCallExpression, Stack<string> pathSegments)
    {
        if (methodCallExpression.Method.Name == "get_Item" && methodCallExpression.Arguments.Count == 1)
        {
            var index = GetIndexFromExpression(methodCallExpression.Arguments[0]);
            pathSegments.Push($"[{index}]");
            return methodCallExpression.Object!;
        }

        throw new NotSupportedException($"Method call '{methodCallExpression.Method.Name}' is not supported. Only indexers are allowed.");
    }

    private static Expression UnpackArrayIndex(BinaryExpression binaryExpression, Stack<string> pathSegments)
    {
        var index = GetIndexFromExpression(binaryExpression.Right);
        pathSegments.Push($"[{index}]");
        return binaryExpression.Left;
    }

    private static int GetIndexFromExpression(Expression argument)
    {
        if (argument is ConstantExpression constantExpression && constantExpression.Value is int constIndex)
        {
            return constIndex;
        }

        // This compiles and executes the expression to get the value of the indexer.
        // This is necessary for indexers that are not literals, e.g. list[i] where i is a variable.
        try
        {
            var objectExpression = Expression.Convert(argument, typeof(object));
            var getter = Expression.Lambda<Func<object>>(objectExpression).Compile();
            var value = getter();

            if (value is int intValue)
            {
                return intValue;
            }
        }
        catch (Exception ex)
        {
            throw new NotSupportedException($"Could not evaluate indexer expression: {argument}", ex);
        }

        throw new NotSupportedException($"Indexer expression '{argument}' did not evaluate to a valid integer index.");
    }
}