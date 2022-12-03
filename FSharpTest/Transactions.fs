(*
1. features untuk tambah different account - ie: bank no 1, bank no 2, cash
2. inter account transaction - so kalau cash out from bank, kita boleh record as well
3. analysis on expenses - ie: percentage based on total expenses (yg ni kau dah ada kan based on gambar atas), comparison period to period (monetary and percentage)
4. expenses reminder - yg ni boleh guna for reminder untuk monthly expenses mcm sewa rumah, bil2, so dia akan remind user untuk bayar expenses ni
5. income as well - just features untuk record income as well, so dapat track accurate balance
6. statement - features to generate personal statement bulanan or any period yg user nak
*)
module Transactions

open System.Data.SQLite
open FinanceTypes

    type TransactionsTypes =
        | Entry of FinanceEntry
        | Debit of FinanceEntry
        | Credit of FinanceEntry
    let transactTypeHelper (entry:FinanceEntry) =
        match entry.Amount with 
            | amt when amt < 0.0 -> Debit(entry)
            | amt when amt >= 0.0 -> Credit(entry)
            | _ -> Credit(entry)
     //Records Transactions

    let recordTransaction (conn:SQLiteConnection) (transactionType: TransactionsTypes) =
        
        let transactionRecordHelper transactionType =
            match transactionType with
                | Entry entry  -> $"Item {entry.EntryName} was added to the list on {entry.EntryDate}",entry.EntryDate
                | Debit entry  -> $"RM {entry.Amount} was deducted from Account on {entry.EntryDate}",entry.EntryDate
                | Credit entry -> $"RM {entry.Amount} was credited to Account on {entry.EntryDate}",entry.EntryDate
        let transactStr,transactDate = transactionRecordHelper transactionType
        //I'll fix this someday. SQL INJECTION VULNERABILITY 
        let query = $"INSERT INTO TRANSACTIONS(transaction_record,date) VALUES(\"{transactStr}\",\"{transactDate}\")"
        let reader = new SQLiteCommand(query,conn) |> fun  x  -> x.ExecuteNonQuery() 
       
        ()

        
