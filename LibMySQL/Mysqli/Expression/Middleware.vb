﻿#Region "Microsoft.VisualBasic::adddf7d79db3581f1b6b8065a391e6bc, LibMySQL\Mysqli\Expression\Middleware.vb"

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

'     Structure FieldArgument
' 
'         Constructor: (+1 Overloads) Sub New
'         Function: ToString
'         Operators: <=, >=
' 
'     Structure WhereArgument
' 
'         Function: GetSQL, ToString
' 
' 
' /********************************************************************************/

#End Region

Imports Microsoft.VisualBasic.Scripting

Namespace Expressions

    Public Structure FieldArgument

        ReadOnly Name$

        Sub New(name$)
            Me.Name = name
        End Sub

        Public Overrides Function ToString() As String
            Return Name
        End Function

        Public Shared Operator <=(field As FieldArgument, value As Object) As String
            Return $"`{field.Name}` = '{InputHandler.ToString(value)}'"
        End Operator

        Public Shared Operator >=(field As FieldArgument, value As Object) As String
            Throw New NotImplementedException
        End Operator

        Public Shared Widening Operator CType(name$) As FieldArgument
            Return New FieldArgument(name)
        End Operator
    End Structure

    Public Structure WhereArgument(Of T As {New, MySQLTable})

        Dim table As Table(Of T)
        Dim condition$

        ''' <summary>
        ''' + 只有一个元素的时候： LIMIT n
        ''' + 只有两个元素的时候： LIMIT m,n
        ''' + 没有任何元素的时候： 无数量限制
        ''' </summary>
        Dim limits As Integer()

        Public Function GetSQL(Optional scalar As Boolean = False) As String
            If scalar Then
                Return $"SELECT * FROM `{table.Schema.TableName}` WHERE {condition} LIMIT 1;"
            Else
                If limits.IsNullOrEmpty Then
                    Return $"SELECT * FROM `{table.Schema.TableName}` WHERE {condition};"
                ElseIf limits.Length = 1 Then
                    Return $"SELECT * FROM `{table.Schema.TableName}` WHERE {condition} LIMIT {limits(0)};"
                Else
                    Return $"SELECT * FROM `{table.Schema.TableName}` WHERE {condition} LIMIT {limits(0)},{limits(1)};"
                End If
            End If
        End Function

        Public Overrides Function ToString() As String
            Return condition
        End Function
    End Structure
End Namespace
