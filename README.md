# Nametag Printer

This is a pet project to print a name tag that will fit into a cheap acrylic name plate holder. 
These can be found on Amazon, and are economically bought in a bundle of to buy a bunch 24.
They can accept a folder 8.5"x11" sheet of paper, the trick is printing in the exact region that will be seen once folded.
This project aims to simplify layout to match the holder, with some adjustability so that it can be customized for different people. 

<a href="https://www.amazon.com/gp/product/B09KXXPRRM?ie=UTF8&psc=1&linkCode=li2&tag=halfemptyorg-20&linkId=da70184524b672c4dd7aa3dda967b103&language=en_US&ref_=as_li_ss_il" target="_blank"><img border="0" src="//ws-na.amazon-adsystem.com/widgets/q?_encoding=UTF8&ASIN=B09KXXPRRM&Format=_SL160_&ID=AsinImage&MarketPlace=US&ServiceVersion=20070822&WS=1&tag=halfemptyorg-20&language=en_US" ></a><img src="https://ir-na.amazon-adsystem.com/e/ir?t=halfemptyorg-20&language=en_US&l=li2&o=1&a=B09KXXPRRM" width="1" height="1" border="0" alt="" style="border:none !important; margin:0px !important;" />

## Running

The project is written in .Net and most easily by running the `dotnet` command.

### Examples
```shell
dotnet run --templateData "FullName=My Name" "Team=Best Team Ever"
```

The above will output to result.pdf

Here's and example with all the features in place:

```shell
dotnet run --verbose --output JustinNametag.pdf --templateData "FullName=Justin Ryan" "Team=Account Identity" "TagLine=Owls are the best" "HotPot=3" "Boba=1"
```

### Arguments

| Argument           | Required | Description                                                       |
|--------------------|----------|-------------------------------------------------------------------|
| -v, --verbose      | No       | Set output to verbose messages.                                   |
| -b, --borderless   | No       | Print an ideal borderless layout, only works with inkjet printers |
| -t, --templateData | Yes      | Template fields pairs, See below for valid values                 |
| -o, --output       | No       | File to save output to, defaults to result.pdf                    |
| --help             | No       | Display this help screen.                                         |
| --version          | No       | Display version information.                                      |

The `templateData` argument drives the template, of which there is currently only one. Refer to Figma template below.

## Printing

Print the output without any scaling.

## Folding

In an ideal mode is to print at the bottom of the page with no margins, but that's not possible on laser printers.
To compensate for this, the default mode is to shift the name tag up, so the background can fill the top and bottom.
The sides will still get cut off though, you can optionally cut them off. Start with the print out:

![Full Sheet](./docs/20220626_230939.jpg)

### Step 1
Flip the paper over.

### Step 2

Fold in half and open back up.

![Fold in Half](./docs/20220626_231042.jpg)

### Step 3

Folder ends into the middle.

![Fold in](./docs/20220626_231153.jpg)

### Step 4

Flip over and slide into hold.

![Stand Up](./docs/20220626_231220.jpg)

### Borderless

To folder from a borderless mode, fold the paper in half, twice.
## Templates

### Figma

This template takes these arguments:

| Argument | Meaning                                              | Required/Default |
|----------|------------------------------------------------------|------------------|
| FullName | Main name that will take up the most space           | Yes              |
| Team     | Subheading shown along the top                       | Yes              |
| TagLine  | Small heading                                        | No               |
| HotPot   | How spicy do you like your hotpot? On a level of 1-3 | No, 3            |
| Boba     | What ice level do you like in your Boba? 1-3         | No, 3            |

## TODO

* Dynamic layout for Hot Pot and Boba, so it can handle more spiciness
* Support different images