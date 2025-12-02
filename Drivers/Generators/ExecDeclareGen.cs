using Microsoft.CodeAnalysis.CSharp.Syntax;
using Plugin;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SqlcGenCsharp.Drivers.Generators;

public class ExecDeclareGen(DbDriver dbDriver)
{
    private CommonGen CommonGen { get; } = new(dbDriver);

    public MemberDeclarationSyntax Generate(
        string queryTextConstant,
        string argInterface,
        Query query
    )
    {
        var parametersStr = CommonGen.GetMethodParameterList(argInterface, query.Params);
        return ParseMemberDeclaration(
            $$"""
            public async Task {{query.Name}}({{parametersStr}})
            {
                {{GetMethodBody(queryTextConstant, query)}}
            }
            """
        )!;
    }

    private string GetMethodBody(string queryTextConstant, Query query)
    {
        var sqlTextTransform = CommonGen.GetSqlTransformations(query, queryTextConstant);
        var useDapper = dbDriver.Options.UseDapper;

        var dapperParams = useDapper ? CommonGen.ConstructDapperParamsDict(query) : string.Empty;
        var sqlVar =
            sqlTextTransform != string.Empty
                ? Variable.TransformedSql.AsVarName()
                : queryTextConstant;
        var transactionProperty = Variable.Transaction.AsPropertyName();
        var dataSourceProperty = Variable.DataSource.AsPropertyName();

        var datasourceCheck = dbDriver.Options.DriverName == DriverName.Npgsql
            ? $$"""
               if ({{dataSourceProperty}} == null)
                   throw new InvalidOperationException("Transaction is null, but datasource is also null.");
            """
            : string.Empty;
        var noTxBody = useDapper
            ? GetDapperNoTxBody(sqlVar, query)
            : GetDriverNoTxBody(sqlVar, query);
        var withTxBody = useDapper
            ? GetDapperWithTxBody(sqlVar, query)
            : GetDriverWithTxBody(sqlVar, query);

        return $$"""
               {{sqlTextTransform}}
               {{dapperParams}}
               if ({{transactionProperty}} == null)
               {
                   {{datasourceCheck}}
                   {{noTxBody}}
               }
               {{withTxBody}}
            """;
    }

    private string GetDapperNoTxBody(string sqlVar, Query query)
    {
        var (establishConnection, _) = dbDriver.EstablishConnection(query);
        var dapperArgs = query.Params.Any()
            ? $", {Variable.QueryParams.AsVarName()}"
            : string.Empty;
        return $$"""
               using ({{establishConnection}})
                   await {{Variable.Connection.AsVarName()}}.ExecuteAsync({{sqlVar}}{{dapperArgs}});
               return;
            """;
    }

    private string GetDapperWithTxBody(string sqlVar, Query query)
    {
        var transactionProperty = Variable.Transaction.AsPropertyName();
        var dapperArgs = query.Params.Any()
            ? $", {Variable.QueryParams.AsVarName()}"
            : string.Empty;
        return $$"""
               {{dbDriver.TransactionConnectionNullExcetionThrow}}
               await this.{{transactionProperty}}.Connection.ExecuteAsync(
                       {{sqlVar}}{{dapperArgs}},
                       transaction: this.{{transactionProperty}});
            """;
    }

    private string GetDriverNoTxBody(string sqlVar, Query query)
    {
        var innerBody =
            $"await {Variable.Command.AsVarName()}.ExecuteNonQueryAsync();\n        return;";
        return dbDriver.GenerateNoTransactionMethodBody(sqlVar, query, innerBody);
    }

    private string GetDriverWithTxBody(string sqlVar, Query query)
    {
        var innerBody = $"await {Variable.Command.AsVarName()}.ExecuteNonQueryAsync();";
        return dbDriver.GenerateWithTransactionMethodBody(sqlVar, query, innerBody);
    }
}