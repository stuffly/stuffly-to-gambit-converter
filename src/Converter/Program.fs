// Let's clarify the terminology.
// The folder that has to be converted is called Stuffly Folder.
// Every non-empty textual file in it is called Stuffly File.
// Every line in a Stuffly File is called Stuffly Item.
// Stuffly Item can have two Parts separated by a vertical line (|).
// Parts are called Left Part and Right Part.
// Once parsed, a Stuffly File is called Stuffly Document.
// Each Stuffly File is converted to a Gambit Deck.
// Each Stuffly Item in the file is converted to a Card within the Deck.
// Each Card has Front Page Side and a Back Page Side.
// The left part of a Stuffly Item is mapped to the Back Page Side.
// The right part of a Stuffly Item is mapped to the Front Page Side.

open System.IO

let getStufflyFolder argv =
    match argv with
        | [] -> Directory.GetCurrentDirectory()
        | [directory] -> directory
        | _ -> argv.Item 0

let getStufflyFilesWithin stufflyFolder =
    (new DirectoryInfo(stufflyFolder)).EnumerateFiles("*.txt", SearchOption.AllDirectories)
        |> Seq.map (fun file -> file.FullName)

let readStufflyFile filePath =
    (
        Path.GetFileNameWithoutExtension(filePath),
        File.ReadAllLines(filePath) |> Array.toList
    )

open System
open System.Text.RegularExpressions

let parseStufflyFile (fileName : string, fileContent) =
    let parseStufflyItem (item : string) =
        
        let replaceRegexMatchWithEmptyString regex =
            let regex = new Regex(regex)
            let replaceRegexMatchWithEmptyStringImplementation = fun (leftPart, rightPart) -> (regex.Replace(leftPart, ""), rightPart)
            replaceRegexMatchWithEmptyStringImplementation

        let removeDate = replaceRegexMatchWithEmptyString @"^\d\d\d\d\.\d\d\.\d\d"

        let removeTags = replaceRegexMatchWithEmptyString "\s#[\w-]+"

        let removeSources = replaceRegexMatchWithEmptyString "\s@[\w-]+"

        let trimParts (leftPart : string, rightPart : string) =
            (leftPart.Trim(), rightPart.Trim())

        let createRightPartIfEmpty (leftPart, rightPart) =
            let shuffle =
                let random = new Random()

                let rec shuffleImplementation (text : string) =
                    let shuffledWords =
                        text.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
                            |> Array.map (fun word -> (random.Next(), word))
                            |> Array.sortBy (fun (index, word) -> index)
                            |> Array.map (fun (index, word) -> word)
                    if shuffledWords.Length = 1 // If the left part has just a single word...
                    then
                        shuffledWords.[0] // ... then we have to return that word.
                        // We cannot return the original 'text' here because it could be that
                        // it had leading or trailing spaces that are removed when the word was
                        // split.
                    else
                        let shuffledText = String.Join(" ", shuffledWords)
                        if shuffledText <> text then shuffledText else shuffleImplementation text

                shuffleImplementation

            match trimParts (leftPart, rightPart) with
                | ("", "") -> (leftPart, rightPart)
                | (_, "") -> (leftPart, sprintf "<<%s>>" (shuffle leftPart))
                | _ -> (leftPart, rightPart)

        let parts =
            match (item.Split('|') |> Array.toList) with
                | [] -> ("", "")
                | [head] -> (head, "")
                | head::tail -> (head, String.Join("|", tail))

        parts
            |> removeDate
            |> removeTags
            |> removeSources
            |> createRightPartIfEmpty
            |> trimParts

    let parsedDocument =
        fileContent
            |> List.map parseStufflyItem
            |> List.where (fun parts -> parts <> ("", "") )
    (fileName, parsedDocument)


open FSharp.Data.Sql
open System.Reflection

// SalDataProvider type provider plays well with the SQLite NuGet package. For more info see the below links:
//      http://blog.wezeku.com/2016/12/17/f-sqlprovider-and-sqlite-now-work-better-together/
//      https://system.data.sqlite.org/index.html/tktview/d4728aecb76adffb227e1bfd5350e81f3cbee7a7
let [<Literal>] resolutionPath = __SOURCE_DIRECTORY__ + @"..\..\packages\System.Data.SQLite.Core.1.0.105.2\lib\net46"
let [<Literal>] connectionString = "Data Source=" + __SOURCE_DIRECTORY__ + @"\GambitDatabase.db;Version=3"

type GambitDatabase = SqlDataProvider< 
                            ConnectionString = connectionString,
                            DatabaseVendor = Common.DatabaseProviderTypes.SQLITE,
                            ResolutionPath = resolutionPath,
                            IndividualsAmount = 1000,
                            UseOptionTypes = true>


let createOutputDatabaseWithin stufflyFolder =    
    let outputDatabaseFileName = Path.Combine(stufflyFolder, "Output.db")
    if File.Exists(outputDatabaseFileName) then
        File.Delete(outputDatabaseFileName)

    File.Copy(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "GambitDatabase.db"), outputDatabaseFileName)

    sprintf "Data Source=%s;Version=3" outputDatabaseFileName


let createDeck (database : GambitDatabase.dataContext) (documentName, parts) =
    let newDeck = database.Main.Decks.Create()
    newDeck.Title <- documentName
    newDeck.CurrentCardIndex <- 0
    (newDeck, parts)


let createDeckCards (database : GambitDatabase.dataContext) (deckId, parts) =
    let createNewCard (database : GambitDatabase.dataContext) index deckId (left, right) =
        let newCard = database.Main.Cards.Create()
        newCard.DeckId <- deckId
        newCard.FrontPageSide <- right
        newCard.BackPageSide <- left
        newCard.OrderIndex <- index
        ()

    parts |> List.iteri (fun index part -> createNewCard database index deckId part)

[<EntryPoint>]
let main argv = 

    let stufflyFolder = getStufflyFolder (Array.toList argv)
    
    let stufflyDocuments = 
        getStufflyFilesWithin stufflyFolder
            |> Seq.map (readStufflyFile >> parseStufflyFile)
            |> Seq.where (fun (documentName, documentContent) -> documentContent <> [])

    let outputDatabaseConnectionString = createOutputDatabaseWithin stufflyFolder

    let database = GambitDatabase.GetDataContext(outputDatabaseConnectionString)

    let decks =
        stufflyDocuments
            |> Seq.map (createDeck database)
            |> Seq.toList // We need the list here in order not to reenumerate the sequence below when we create cards.

    // We have to save the decks so that they get the IDs updated before we create their cards.
    database.SubmitUpdates()

    decks
        |> List.map (fun deck -> ((fst deck).Id, snd deck))        
        |> List.iter (createDeckCards database)

    database.SubmitUpdates()

    0