# Stuffly to Gambit Converter

Converts Stuffly Folders to [Gambit Flashcards](https://play.google.com/store/apps/details?id=ru.ming13.gambit) Decks and Cards.

[![Build status](https://ci.appveyor.com/api/projects/status/etrgjgh3q5u3eqpy?svg=true)](https://ci.appveyor.com/project/ironcev/stuffly-to-gambit-converter)
[![Test status](http://teststatusbadge.azurewebsites.net/api/status/ironcev/stuffly-to-gambit-converter)](https://ci.appveyor.com/project/ironcev/stuffly-to-gambit-converter)

## About
*Stuffly to Gambit Converter* convertes Stuffly Folders to an SQLite database compatible with [Gambit Flashcards](https://play.google.com/store/apps/details?id=ru.ming13.gambit) version 1.2.0.

Each Stuffly File is converted into a Gambit Deck.

![Gambit Flashcards Decks](doc/converting-stuffly-files-to-gambit-flashcards-decks.jpg)

Each line in the Stuffly File (Stuffly Item) is converted into a single Card within the Deck.

![Gambit Flashcards Cards](doc/converting-stuffly-items-to-gambit-flashcards-cards.jpg)

## Usage
If the *Stuffly to gambit Converter* is registered in your PATH system variable just run it within your Stuffly Folder:

    c:\Path\To\My\Stuffly\Folder\StufflyToGambitConverter.exe

Otherwise, run the converter out of its folder and provide the Stuffly Folder as the first and only command line parameter:

    c:\StufflyToGambitConverter\StufflyToGambitConverter.exe "c:\Path\To\My\Stuffly\Folder"

**NOTE: The path to the Stuffly Folder must not end with backspace.**

The converter will create a file called *Output.db* that can be imported into the [Gambit Flashcards](https://play.google.com/store/apps/details?id=ru.ming13.gambit).

## Known Limitations
The [latest version (0.2.2)](https://github.com/ironcev/stuffly-to-gambit-converter/releases/tag/v0.2.2) of the *Stuffly to Gambit Converter* covers the most important use case of converting a whole Stuffly Folder at once. Here are the main known limitations:

- The Stuffly Folder must not contain two or more Stuffly Files with the same name.
- All the Stuffly Files within the Stuffly Folder will be converted. It is not possible to convert specific Stuffly Files or to exclude certain files.
- There is no logging or error handling. If anything goes wrong, the application will simply crash.

## License

*Stuffly to gambit Converter* is licensed under the [MIT license](LICENSE).