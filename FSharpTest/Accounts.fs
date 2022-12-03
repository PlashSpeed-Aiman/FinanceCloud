
module Accounts
    //What can accounts do?
    //Deduct Balance
    //Add Balance
    //Transfer to other accounts
open System.Data.SQLite
open FinanceTypes
open Transactions

    type Account = {Name:string;Balance:double}
    let recordBalance conn account = 
        let query = 
            $"UPDATE accounts
            SET balance = {account.Balance} WHERE name is \"{ account.Name.ToLower()}\""

        let reader = new SQLiteCommand(query,conn) |> fun  x  -> x.ExecuteNonQuery() 
        ()
    let updateBalance  (account:Account) (transcationType:TransactionsTypes) =
        match transcationType with
            | Debit entry ->  {account with Balance = account.Balance + entry.Amount}
            | Credit entry ->  {account with Balance = account.Balance + entry.Amount}
            | _ -> {account with Name=account.Name; Balance=account.Balance}

