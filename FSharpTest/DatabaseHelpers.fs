﻿
module DatabaseHelpers

open System.Data.SQLite

let createTransactTable conn = 
    let structureSql = "CREATE TABLE IF NOT EXISTS transactions(transaction_record TEXT, date TEXT)"
    let structureCommand = new SQLiteCommand(structureSql, conn)
    let _ = structureCommand.ExecuteNonQuery()
    ()
let createAccountsTable conn = 
    let structureSql = "CREATE TABLE IF NOT EXISTS accounts(name TEXT, balance REAL)"
    let structureCommand = new SQLiteCommand(structureSql, conn)
    let _ = structureCommand.ExecuteNonQuery()
    ()
let createFinanceTable conn = 
    let structureSql = "CREATE TABLE IF NOT EXISTS finance(entry_name TEXT, amount REAL, entry_date TEXT,category TEXT)"
    let structureCommand = new SQLiteCommand(structureSql, conn)
    let _ = structureCommand.ExecuteNonQuery()
    ()
let createTableMany conn = 
    let () = createFinanceTable conn  
    let () = createAccountsTable conn
    let () = createTransactTable conn
    ()