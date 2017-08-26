﻿// Let's clarify the terminology.
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

let readStufflyFile stufflyFilePath =
    (
        Path.GetFileNameWithoutExtension(stufflyFilePath),
        File.ReadAllLines(stufflyFilePath) |> Array.toList
    )

open System
open System.Text.RegularExpressions

let parseStufflyFile (stufflyFileName : string, stufflyFileContent) =
    let parseStufflyItem (item : string) =

        let removeDate (leftPart, rightPart) =
            (Regex.Replace(leftPart, @"^\d\d\d\d\.\d\d\.\d\d", ""), rightPart)

        let removeTags (leftPart, rightPart) =
            (Regex.Replace(leftPart, "\s#[\w-]+", ""), rightPart)

        let removeSources (leftPart, rightPart) =
            (Regex.Replace(leftPart, "\s@[\w-]+", ""), rightPart)

        let trimParts (leftPart : string, rightPart : string) =
            (leftPart.Trim(), rightPart.Trim())

        let createRightPartIfEmpty (leftPart, rightPart) =
            let shuffle =
                let random = new Random()

                let rec innerShuffle (text : string) =
                    let shuffledWords =
                        text.Split(' ')
                            |> Array.map (fun word -> (random.Next(), word))
                            |> Array.sortBy (fun (index, word) -> index)
                            |> Array.map (fun (index, word) -> word)
                    if shuffledWords.Length = 1 // If the left part has just a single word...
                    then
                        text // ... then we have to return that word.
                    else
                        let shuffledText = String.Join(" ", shuffledWords)
                        if shuffledText <> text then shuffledText else innerShuffle text

                innerShuffle

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
        stufflyFileContent
            |> List.map (fun stufflyItem -> parseStufflyItem stufflyItem)
            |> List.where (fun parts -> parts <> ("", "") )
    (stufflyFileName, parsedDocument)


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
            |> Seq.map readStufflyFile
            |> Seq.map parseStufflyFile
            |> Seq.where (fun (documentName, documentContent) -> documentContent <> [])

    let outputDatabaseConnectionString = createOutputDatabaseWithin stufflyFolder

    let database = GambitDatabase.GetDataContext(outputDatabaseConnectionString)

    let decks =
        stufflyDocuments
            |> Seq.map (fun document -> createDeck database document)
            |> Seq.toList // We need the list here in order not to reenumerate the sequence below when we create cards.

    // We have to save the decks so that they get the IDs updated before we create their cards.
    database.SubmitUpdates()

    decks
        |> List.map (fun deck -> ((fst deck).Id, snd deck))
        |> List.iter (fun deckIdAndParts -> createDeckCards database deckIdAndParts)

    database.SubmitUpdates()

    0