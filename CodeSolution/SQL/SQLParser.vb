﻿#Region "Microsoft.VisualBasic::48960782e360995cf3c6f912bfa3c6b7, CodeSolution\SQL\SQLParser.vb"

' Author:
' 
'       asuka (amethyst.asuka@gcmodeller.org)
'       xie (genetics@smrucc.org)
'       xieguigang (xie.guigang@live.com)
' 
' Copyright (c) 2018 GPL3 Licensed
' 
' 
' GNU GENERAL PUBLIC LICENSE (GPL3)
' 
' 
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
' 
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
' GNU General Public License for more details.
' 
' You should have received a copy of the GNU General Public License
' along with this program. If not, see <http://www.gnu.org/licenses/>.



' /********************************************************************************/

' Summaries:

' Module SQLParser
' 
'     Function: __createDataType, (+2 Overloads) __createField, __createSchema, __createSchemaInner, __getDBName
'               __getNumberValue, __parseTable, __splitInternal, __sqlParser, (+2 Overloads) LoadSQLDoc
'               LoadSQLDocFromStream, ParseTable
' 
' /********************************************************************************/

#End Region

Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Text.RegularExpressions
Imports Microsoft.VisualBasic.ComponentModel.DataSourceModel
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Linq
Imports Microsoft.VisualBasic.Text
Imports Oracle.LinuxCompatibility.MySQL.Reflection.Schema
Imports r = System.Text.RegularExpressions.Regex

Public Module SQLParser

    ''' <summary>
    ''' Parsing the create table statement in the SQL document.
    ''' </summary>
    Const SQL_CREATE_TABLE As String = "CREATE TABLE (IF\s+NOT\s+EXISTS\s+)?(`.+?`\.)?`.+?` \(.+?(PRIMARY KEY \(`.+?`\))?.+?ENGINE\s*=.+?;"

    Public Function ParseTable(SQL As String) As Reflection.Schema.Table
        Dim CTMatch As Match = r.Match(SQL, SQL_CREATE_TABLE, RegexOptions.Singleline)
        Dim Tokens As NamedValue(Of String()) = __sqlParser(CTMatch.Value.Replace(vbLf, vbCr))

        Try
            Return __parseTable(SQL, CTMatch, Tokens)
        Catch ex As Exception
            Dim dump As StringBuilder = New StringBuilder
            Call dump.AppendLine(SQL)
            Call dump.AppendLine(vbCrLf)
            Call dump.AppendLine(NameOf(CTMatch) & "   ====> ")
            Call dump.AppendLine(CTMatch.Value)
            Call dump.AppendLine(vbCrLf)
            Call dump.AppendLine($"TableName:={Tokens.Name}")
            Call dump.AppendLine(New String("-"c, 120))
            Call dump.AppendLine(vbCrLf)
            Call dump.AppendLine(String.Join(vbCrLf & "  >  ", Tokens.Value))

            Throw New Exception(dump.ToString, ex)
        End Try
    End Function

    Private Function __parseTable(SQL As String, CTMatch As Match, Tokens As NamedValue(Of String())) As Reflection.Schema.Table
        Dim DB As String = __getDBName(SQL)
        Dim TableName As String = Tokens.Value(Scan0)
        Dim PrimaryKey As String = Tokens.Name
        Dim FieldsTokens As String() = Tokens.Value.Skip(1).ToArray
        Dim Table As Table = SetValue(Of Table).InvokeSet(
                __createSchema(FieldsTokens,
                               TableName,
                               PrimaryKey,
                               SQL),
                NameOf(Reflection.Schema.Table.Database),
                DB)
        Return Table
    End Function

    ''' <summary>
    ''' Loading the table schema from a specific SQL doucment.
    ''' </summary>
    ''' <param name="path"></param>
    ''' <returns></returns>
    Public Function LoadSQLDoc(path$) As Table()
        Using file As New StreamReader(New FileStream(path, FileMode.Open))
            Return file.LoadSQLDoc
        End Using
    End Function

    Public Function LoadSQLDocFromStream(sqlDoc As String) As Table()
        Dim DB As String = __getDBName(sqlDoc)
        Dim tables = (From table As String
                      In sqlDoc.__splitInternal
                      Let tokens As NamedValue(Of String()) = __sqlParser(SQL:=table)
                      Let tableName As String = tokens.Value(Scan0)
                      Let primaryKey As String = tokens.Name
                      Let fieldsTokens = tokens _
                          .Value _
                          .Skip(1) _
                          .ToArray
                      Select primaryKey,
                          tableName,
                          Fields = fieldsTokens,
                          original = table).ToArray
        Dim setValue = New SetValue(Of Table)().GetSet(NameOf(Table.Database))
        Dim SqlSchema = LinqAPI.Exec(Of Table) _
 _
            () <= From table
                  In tables
                  Let tbl As Table = __createSchema(
                      table.Fields,
                      table.tableName,
                      table.primaryKey,
                      table.Original)
                  Select setValue(tbl, DB)

        Return SqlSchema
    End Function

    <Extension>
    Private Function __splitInternal(sql$) As String()
        Dim out$() = r.Matches(sql, SQL_CREATE_TABLE, RegexOptions.Singleline).ToArray
        Return out
    End Function

    <Extension>
    Public Function LoadSQLDoc(stream As StreamReader, Optional ByRef raw As String = Nothing) As Table()
        With stream.ReadToEnd.Replace("<", "&lt;")
            raw = .ByRef
            Return LoadSQLDocFromStream(.ByRef)
        End With
    End Function

    ''' <summary>
    ''' 获取数据库的名称
    ''' </summary>
    ''' <param name="SQL"></param>
    ''' <returns></returns>
    Private Function __getDBName(SQL As String) As String
        Dim name$ = r.Match(SQL, DB_NAME, RegexOptions.IgnoreCase).Value

        If String.IsNullOrEmpty(name) Then
            Return ""
        Else
            name = r.Match(name, "`.+?`").Value
            name = Mid(name, 2, Len(name) - 2)
            Return name
        End If
    End Function

    Const DB_NAME As String = "CREATE\s+((DATABASE)|(SCHEMA))\s+IF\s+NOT\s+EXISTS\s+`.+?`"

    Private Function __sqlParser(SQL As String) As NamedValue(Of String())
        Dim tokens$() = SQL.LineTokens
        Dim p% = tokens.Lookup("PRIMARY KEY")
        Dim primaryKey As String

        If p = -1 Then ' 没有设置主键
            p = tokens.Lookup("UNIQUE KEY")
        End If

        If p = -1 Then
            p = tokens.Lookup("KEY")
        End If

        If p = -1 Then
            primaryKey = ""
        Else
_SET_PRIMARYKEY:
            primaryKey = tokens(p)
            tokens = tokens.Take(p).ToArray
        End If

        p = tokens.Lookup(") ENGINE=")

        If Not p = -1 Then
            tokens = tokens.Take(p).ToArray
        End If

        Return New NamedValue(Of String())(primaryKey, tokens)
    End Function

    ''' <summary>
    ''' Create a MySQL table schema object.
    ''' </summary>
    ''' <param name="Fields"></param>
    ''' <param name="TableName"></param>
    ''' <param name="PrimaryKey"></param>
    ''' <param name="CreateTableSQL"></param>
    ''' <returns></returns>
    Private Function __createSchema(Fields As String(), TableName As String, PrimaryKey As String, CreateTableSQL As String) As Reflection.Schema.Table
        Try
            Return __createSchemaInner(Fields, TableName, PrimaryKey, CreateTableSQL)
        Catch ex As Exception
            Dim dump As StringBuilder = New StringBuilder
            Call dump.AppendLine(NameOf(CreateTableSQL))
            Call dump.AppendLine(New String("="c, 120))
            Call dump.AppendLine(CreateTableSQL)
            Call dump.AppendLine(vbCrLf)
            Call dump.AppendLine($"{NameOf(TableName)}   ===>  {TableName}")
            Call dump.AppendLine($"{NameOf(PrimaryKey)}  ===>  {PrimaryKey}")
            Call dump.AppendLine(vbCrLf)
            Call dump.AppendLine(NameOf(Fields))
            Call dump.AppendLine(New String("="c, 120))
            Call dump.AppendLine(String.Join(vbCrLf, Fields))

            Throw New Exception(dump.ToString, ex)
        End Try
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="Fields"></param>
    ''' <param name="TableName"></param>
    ''' <param name="PrimaryKey"></param>
    ''' <param name="CreateTableSQL">Create table SQL raw.</param>
    ''' <returns></returns>
    Private Function __createSchemaInner(Fields As String(), TableName As String, PrimaryKey As String, CreateTableSQL As String) As Reflection.Schema.Table
        TableName = Regex.Match(TableName, "`.+?`").Value
        TableName = Mid(TableName, 2, Len(TableName) - 2)
        PrimaryKey = Regex.Match(PrimaryKey, "\(`.+?`\)").Value

        Dim PrimaryKeys As String()

        If Not String.IsNullOrEmpty(PrimaryKey) Then
            PrimaryKey = Regex.Replace(PrimaryKey, "\(\d+\)", "")
            PrimaryKey = Mid(PrimaryKey, 2, Len(PrimaryKey) - 2)
            PrimaryKey = Mid(PrimaryKey, 2, Len(PrimaryKey) - 2)
            PrimaryKeys = Strings.Split(PrimaryKey, "`,`")
        Else
            PrimaryKeys = New String() {}
        End If

        Dim Comment As String = Regex.Match(CreateTableSQL, "COMMENT='.+';", RegexOptions.Singleline).Value
        Dim FieldLQuery = (From Field As String
                           In Fields
                           Select __createField(Field)).ToDictionary(Function(Field) Field.FieldName)

        If Not String.IsNullOrEmpty(Comment) Then
            Comment = Mid(Comment, 10)
            Comment = Mid(Comment, 1, Len(Comment) - 2)
        End If

        CreateTableSQL = ASCII.ReplaceQuot(CreateTableSQL, "\'")

        ' The database fields reflection result {Name, Attribute}
        Dim TableSchema As New Table(FieldLQuery) With {
            .TableName = TableName,
            .PrimaryFields = PrimaryKeys.AsList,   ' Assuming at least only one primary key in a table
            .Index = PrimaryKey,
            .Comment = Comment,
            .SQL = CreateTableSQL
        }
        Return TableSchema
    End Function

    ''' <summary>
    ''' Regex expression for parsing the comments of the field in a table definition.
    ''' </summary>
    Const FIELD_COMMENTS As String = "COMMENT '.+?',"

    Private Function __createField(FieldDef As String, Tokens As String()) As Reflection.Schema.Field
        Dim FieldName As String = Tokens(0)
        Dim DataType As String = Tokens(1)
        Dim Comment As String = Regex.Match(FieldDef, FIELD_COMMENTS).Value
        Dim i As Integer = InStr(FieldDef, FieldName)
        FieldDef = Mid(FieldDef, i + Len(FieldName))
        i = InStr(FieldDef, DataType)
        FieldDef = Mid(FieldDef, i + Len(DataType)).Replace(",", "").Trim
        FieldName = Mid(FieldName, 2, Len(FieldName) - 2)

        If Not String.IsNullOrEmpty(Comment) Then
            Comment = Mid(Comment, 10)
            Comment = Mid(Comment, 1, Len(Comment) - 2)
        End If

        Dim p_CommentKeyWord As Integer = InStr(FieldDef, "COMMENT '", CompareMethod.Text)
        Dim p As New int

        If p_CommentKeyWord = 0 Then  '没有注释，则百分之百就是列属性了
            p_CommentKeyWord = Integer.MaxValue
        End If

        Dim IsAutoIncrement As Boolean = (p = InStr(FieldDef, "AUTO_INCREMENT", CompareMethod.Text)) > 0 AndAlso p < p_CommentKeyWord
        Dim IsNotNull As Boolean = (p = InStr(FieldDef, "NOT NULL", CompareMethod.Text)) > 0 AndAlso p < p_CommentKeyWord

        Dim FieldSchema As New Reflection.Schema.Field With {
            .FieldName = FieldName,
            .DataType = __createDataType(DataType.Replace(",", "").Trim),  ' Some data type can be merged into a same type when we mapping a database table
            .Comment = Comment,
            .AutoIncrement = IsAutoIncrement,
            .NotNull = IsNotNull
        }
        Return FieldSchema
    End Function

    Private Function __createField(FieldDef As String) As Reflection.Schema.Field
        Dim name$ = Regex.Match(FieldDef, "`.+?`", RegexICSng).Value
        Dim tokens$() = {name}.Join(FieldDef.Replace(name, "").Trim.Split)
        Try
            Return __createField(FieldDef, tokens)
        Catch ex As Exception
            Throw New Exception($"{NameOf(__createField)} ===>  {FieldDef}{vbCrLf & vbCrLf & vbCrLf}", ex)
        End Try
    End Function

    ''' <summary>
    ''' Mapping the MySQL database type and visual basic data type 
    ''' </summary>
    ''' <param name="type_define"></param>
    ''' <returns></returns>
    Private Function __createDataType(type_define$) As Reflection.DbAttributes.DataType
        Dim type As Reflection.DbAttributes.MySqlDbType
        Dim parameter As String = ""

        If Regex.Match(type_define, "tinyint\(\d+\)", RegexOptions.IgnoreCase).Success Then
            parameter = __getNumberValue(type_define)
            If parameter = "1" Then
                ' boolean 
                type = Reflection.DbAttributes.MySqlDbType.Boolean
            Else
                type = Reflection.DbAttributes.MySqlDbType.Int32
            End If

        ElseIf Regex.Match(type_define, "int\(\d+\)", RegexOptions.IgnoreCase).Success Then
            type = Reflection.DbAttributes.MySqlDbType.Int64
            parameter = __getNumberValue(type_define)

        ElseIf Regex.Match(type_define, "varchar\(\d+\)", RegexOptions.IgnoreCase).Success OrElse Regex.Match(type_define, "char\(\d+\)", RegexOptions.IgnoreCase).Success Then
            type = Reflection.DbAttributes.MySqlDbType.VarChar
            parameter = __getNumberValue(type_define)

        ElseIf Regex.Match(type_define, "double", RegexOptions.IgnoreCase).Success OrElse InStr(type_define, "float") > 0 Then
            type = Reflection.DbAttributes.MySqlDbType.Double

        ElseIf Regex.Match(type_define, "datetime", RegexOptions.IgnoreCase).Success OrElse
            Regex.Match(type_define, "date", RegexOptions.IgnoreCase).Success OrElse
            Regex.Match(type_define, "timestamp", RegexOptions.IgnoreCase).Success Then

            type = Reflection.DbAttributes.MySqlDbType.DateTime

        ElseIf Regex.Match(type_define, "text", RegexOptions.IgnoreCase).Success Then
            type = Reflection.DbAttributes.MySqlDbType.Text

        ElseIf InStr(type_define, "enum(", CompareMethod.Text) > 0 Then   ' enum类型转换为String类型？？？？
            type = Reflection.DbAttributes.MySqlDbType.String

        ElseIf InStr(type_define, "Blob", CompareMethod.Text) > 0 OrElse
            Regex.Match(type_define, "varbinary\(\d+\)", RegexOptions.IgnoreCase).Success OrElse
            Regex.Match(type_define, "binary\(\d+\)", RegexOptions.IgnoreCase).Success Then
            type = Reflection.DbAttributes.MySqlDbType.Blob

        ElseIf Regex.Match(type_define, "decimal\(", RegexOptions.IgnoreCase).Success Then
            type = Reflection.DbAttributes.MySqlDbType.Decimal

        ElseIf Regex.Match(type_define, "bit\(", RegexICSng).Success Then
            type = Reflection.DbAttributes.MySqlDbType.Bit
            parameter = __getNumberValue(type_define)

        Else

            'More complex type is not support yet, but you can easily extending the mapping code at here
            Throw New NotImplementedException($"Type define is not support yet for    {NameOf(type_define)}   >>> ""{type_define}""")

        End If

        Return New Reflection.DbAttributes.DataType(type, parameter)
    End Function

    Private Function __getNumberValue(typeDef As String) As String
        Dim parameter As String = Regex.Match(typeDef, "\(.+?\)").Value
        parameter = Mid(parameter, 2, Len(parameter) - 2)
        Return parameter
    End Function
End Module
