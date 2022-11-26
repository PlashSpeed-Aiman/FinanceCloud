// For more information see https://aka.ms/fsharp-console-apps
open System
open System.Collections.Generic
open System.Data.SQLite
open System.IO
open System.Threading.Tasks
open Google.Cloud.Firestore.V1
open Microsoft.FSharp.Core
(*
    Sort by Month, Category, Get Average
    For all expense, print to PDF


*)
open System
open FSharp.Core
open FSharp.Data
open Microsoft.FSharp.Reflection
open XPlot.Plotly
open Google.Cloud.Firestore

type ('a) FirebaseOperationState =
    | FirebaseOperationSuccess of 'a
    | FirebaseOperationFailure of Exception 
type FinanceCategories =
    | Food
    | Lifestyle
    | Utilities
    | Others
type ProgOptions =
    |AddEntry
    |ViewEntries
    |StatsView
    |ExitProg

type FinanceEntry =
    {EntryName:string;Amount:double;EntryDate:string;EntryMonth:int;EntryYear:int;Category:FinanceCategories}

let asMap (recd:'T) = 
  [ for p in FSharpType.GetRecordFields(typeof<'T>) ->
      p.Name, p.GetValue(recd) ]
  |> Map.ofList

//TODO : For syncing with cloud, first try connection, if failed, keep in queue, allow user to sync to cloud explicitly, if success empty queue, if failed, queue is not emptied
let db_connect () =
    let sqlite_conn = new SQLiteConnection("Data Source= fin.db;Version=3;New=True;Compress=True;")
    sqlite_conn.Open()
    sqlite_conn


let MakeGraph (somelist:FinanceEntry list) =
    
    let utilities = List.filter (fun x -> x.Category = Utilities) somelist |> fun x -> [ for elem in x do elem.Amount] |> List.reduce (fun x y -> x + y) |> fun x -> (Utilities.ToString(),x)
    let food      = List.filter (fun x -> x.Category = Food)      somelist |> fun x -> [ for elem in x do elem.Amount] |> List.reduce (fun x y -> x + y) |> fun x -> (Food.ToString(),x)
    let lifestyle = List.filter (fun x -> x.Category = Lifestyle) somelist |> fun x -> [ for elem in x do elem.Amount] |> List.reduce (fun x y -> x + y) |> fun x -> (Lifestyle.ToString(),x)   
    let others    = List.filter (fun x -> x.Category = Others)    somelist |> fun x -> [ for elem in x do elem.Amount] |> List.reduce (fun x y -> x + y) |> fun x -> (Others.ToString(),x)   
    let data = [
            utilities
            food
            lifestyle
            others
            ]
    
    async{
        let chart =
            data
            |> Chart.Pie
            |> Chart.WithTitle "Monthly Expenses"
            |> Chart.WithLegend true
        let () = chart.Show()
        ()
    } |> Async.Start
    ()

let StatsOverview (somelist:FinanceEntry list)=
    let list_utilities = List.filter (fun x -> x.Category = Utilities) somelist |> fun x -> [ for elem in x do elem.Amount]
    let list_food      = List.filter (fun x -> x.Category = Food)      somelist |> fun x -> [ for elem in x do elem.Amount]
    let list_lifestyle = List.filter (fun x -> x.Category = Lifestyle) somelist |> fun x -> [ for elem in x do elem.Amount]
    let list_others    = List.filter (fun x -> x.Category = Others)    somelist |> fun x -> [ for elem in x do elem.Amount]
    let total_list     = [list_others;list_food;list_lifestyle;list_utilities]
    let total_amount   = total_list |> List.map List.sum |> List.reduce (fun x y -> x + y)
    printfn "%A MYR is spent on Food" (List.sum list_food) 
    printfn "%A MYR is spent on Lifestyle" (List.sum list_lifestyle) 
    printfn "%A MYR is spent on Utilities" (List.sum list_utilities) 
    printfn "%A MYR is spent on Others" (List.sum list_others)
    printfn "Total Spent is %A MYR " (total_amount)
    let _ = MakeGraph somelist
    ()


let RetrieveFromDatabase (conn:SQLiteConnection) =
    let querySql = 
        "SELECT * FROM finance"
    use reader = new SQLiteCommand(querySql,conn) |> fun  x  -> x.ExecuteReader()
    let convertStrToDU text =  
        match text with 
            |"Food" -> Food
            |"Lifestyle" -> Lifestyle
            |"Utilities" -> Utilities
            |"Others"|_ -> Others
    let convertDateTime some_date_time_string = 
        let result = DateTime.Parse some_date_time_string
        result
    let rec readHelper (reader:SQLiteDataReader) (alist: FinanceEntry list) =
        match reader.Read() with 
            |false -> alist
            |true ->  {
            EntryName=reader.GetString(0);
            Amount     =  (reader.GetDouble 1 ); 
            EntryDate  =  (reader.GetString 2 );
            EntryMonth =  (reader.GetString 2  |> convertDateTime |> fun x-> x.Month);
            EntryYear  =  (reader.GetString 2  |> convertDateTime |> fun x-> x.Year);
            Category   =  (reader.GetString 3  |> convertStrToDU) }::alist 
            |> readHelper reader
             
    let res = readHelper reader [] |> List.filter (fun x -> x.EntryMonth = DateTime.Now.Month)
    List.iter (fun x -> printfn "ENTRY NAME: %A\nAMOUNT: %A\nENTRY DATE: %A\n" x.EntryName x.Amount x.EntryDate) res 
    let _ = StatsOverview res 

    ()

let SendToDatabase  (conn:SQLiteConnection) (item:FinanceEntry) :unit  = 
    
    let insertSql = 
        $"INSERT INTO finance(entry_name, amount, entry_date,category) " + 
        $"""values ("{item.EntryName}",{item.Amount},"{item.EntryDate}","{item.Category}")"""
    
    let reader = new SQLiteCommand(insertSql,conn) |> fun  x  -> x.ExecuteNonQuery() 
    
    let input:Map<string,obj> = FSharp.Collections.Map [ ("EntryName", item.EntryName); ("Amount", item.Amount);("EntryDate",item.EntryDate);("EntryMonth",item.EntryMonth);("EntryYear",item.EntryYear);("Category",item.Category.ToString())]
    
    let db = FirestoreDb.Create("shining-weft-357007") |> fun db -> db.Collection("finance")
   
    let sendHelper  =
        async{
          let x = db.AddAsync(input)
          ()
        }
        //TODO : ERROR HANDLING
    sendHelper  |> Async.Start
    
    ()
    
let ProgramSelect () =
    Console.WriteLine "Finance App\n1.Add Entry\n2.View Monthly Entries"
    let value = Console.ReadLine()
    let helperfunc value =
        match value with
            |"1" -> AddEntry
            |"2" -> ViewEntries
            |"3" -> StatsView
            |"4"|_ -> ExitProg
    let progOp = helperfunc value
    progOp

let AddEntryFunc conn =
    Console.WriteLine "Insert Entry Name"
    let entryname = Console.ReadLine()
    Console.WriteLine "Insert Amount"
    let amount = Console.ReadLine()
    Console.WriteLine "Category\n1.Food\n2.Lifestyle\n3.Utilities\n4.Others"
    let category_val = Console.ReadLine()
    let helperfunc category_val =
        match category_val with
            |"1" -> Food
            |"2" -> Lifestyle
            |"3" -> Utilities
            |"4"|_ -> Others
    let category_val_DU = helperfunc category_val
    let entry_item  = {EntryName=entryname;Amount= (Double.Parse amount); EntryDate =  DateTime.Today.ToShortDateString();EntryMonth= DateTime.Today.Month;EntryYear= DateTime.Today.Year;Category=category_val_DU }
    SendToDatabase conn entry_item
    ()

[<EntryPoint>]
let main argv =
//Please apply the Open Late, Close Early Principle for SQLite Connections
    let testvar = 3
    let conn = db_connect ()
    let mutable app_condition = false
    while not app_condition do 
        let res = ProgramSelect()
        match res with
            |AddEntry -> AddEntryFunc conn
            |ViewEntries -> RetrieveFromDatabase conn
            |ExitProg -> app_condition <- true
            |_ -> app_condition <- true
        () 
    let _ = conn.Dispose()
    0


