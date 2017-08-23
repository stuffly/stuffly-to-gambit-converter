open System.IO

let getStufflyFiles workingDirectory =
    (new DirectoryInfo(workingDirectory)).EnumerateFiles("*.txt", SearchOption.AllDirectories)
        |> Seq.where (fun file -> file.Length > 10L)
        |> Seq.map (fun file -> file.FullName)

let readStufflyDocumentFromFile filePath =
    (
        Path.GetFileNameWithoutExtension(filePath),
        File.ReadAllLines(filePath) |> Array.toList
    )

open System
open System.Text.RegularExpressions

let parseStufflyItem (item : String) =
    let sides = item.Split('|') |> Array.toList
    match sides with
        | [] -> ("", "")
        | first::rest ->             
            let backPage = Regex.Replace(first, "^\d\d\d\d\.\d\d\.\d\d\ ", "")
            (String.Join("|", rest), backPage)


let parseStufflyDocument (documentName : String, documentContent) =
    let parsedDocument =
        documentContent
            |> List.map (fun stufflyItem -> parseStufflyItem stufflyItem)
            |> List.where (fun sides -> snd sides <> "" )
    (documentName, parsedDocument)


open FSharp.Data.Sql
open System.Reflection

// SalDataProvider type provider plays well with the SQLite NuGet package. For more info see the below links:
//      http://blog.wezeku.com/2016/12/17/f-sqlprovider-and-sqlite-now-work-better-together/
//      https://system.data.sqlite.org/index.html/tktview/d4728aecb76adffb227e1bfd5350e81f3cbee7a7
let [<Literal>] resolutionPath = __SOURCE_DIRECTORY__ + @"..\packages\System.Data.SQLite.Core.1.0.105.2\lib\net46"
let [<Literal>] connectionString = "Data Source=" + __SOURCE_DIRECTORY__ + @"\GambitDatabase.db;Version=3"

type GambitDatabase = SqlDataProvider< 
                            ConnectionString = connectionString,
                            DatabaseVendor = Common.DatabaseProviderTypes.SQLITE,
                            ResolutionPath = resolutionPath,
                            IndividualsAmount = 1000,
                            UseOptionTypes = true>


let createOutputDatabase workingDirectory =
    let outputDatabaseFileName = Path.Combine(workingDirectory, "Output.db")
    if File.Exists(outputDatabaseFileName) then
        File.Delete(outputDatabaseFileName)

    File.Copy(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "GambitDatabase.db"), outputDatabaseFileName)
    ()


let createNewDeck (database : GambitDatabase.dataContext) (documentName, cards) =
    let newDeck = database.Main.Decks.Create()
    newDeck.Title <- documentName
    newDeck.CurrentCardIndex <- 0
    (newDeck, cards)


let createDeckCards (database : GambitDatabase.dataContext) (deckId, cards) =
    let createNewCard (database : GambitDatabase.dataContext) deckId (front, back) =
        let newCard = database.Main.Cards.Create()
        newCard.DeckId <- deckId
        newCard.FrontPageSide <- front
        newCard.BackPageSide <- back
        newCard.OrderIndex <- 0
        ()

    cards |> List.iter (fun card -> createNewCard database deckId card)

let getWorkingDirectory argv =
    match argv with
        | [] -> Directory.GetCurrentDirectory()
        | [directory] -> directory
        | _ -> argv.Item 0

[<EntryPoint>]
let main argv = 

    let workingDirectory = getWorkingDirectory (Array.toList argv)
    
    let stufflyFiles = getStufflyFiles workingDirectory

    let parsedStufflyDocuments = 
        stufflyFiles
            |> Seq.map readStufflyDocumentFromFile
            |> Seq.map parseStufflyDocument

    createOutputDatabase workingDirectory

    let database = GambitDatabase.GetDataContext( sprintf "Data Source=%s;Version=3" (Path.Combine(workingDirectory, "Output.db")) )

    let decks =
        parsedStufflyDocuments
            |> Seq.map (fun document -> createNewDeck database document)
            |> Seq.toList // We need the list here in order not to reenumerate the sequence below when we create cards.

    // We have to save the decks so that they get the IDs updated before we create
    // their cards.
    database.SubmitUpdates()

    decks
        |> List.map (fun deck -> ((fst deck).Id, snd deck))
        |> List.iter (fun deck -> createDeckCards database deck)

    database.SubmitUpdates()

    0