###################################################
COMET - basic COM terminal
.NET 4.0
J.Simon 11/2015
###################################################

COMET is a very basic COM Serial terminal with slight automation capability.

Commands that are entered into the terminal are automatically added as buttons to a pane on the right. 
The checkbox next to the command terminal turns this feature on and off.

The command history can be saved and reloaded using File>Save and File>Open.


!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
Saved History file format:
Each history button is saved in a text file in the following format (Tab delimited):

<COMMAND><TAB><TAB><TAB><TAB><DESCRIPTION>
<COMMAND><TAB><TAB><TAB><TAB><DESCRIPTION>
<COMMAND><TAB><TAB><TAB><TAB><DESCRIPTION>

Each command is on its own line;  multiple tabs are treated as one.
The COMMAND that will be sent to the terminal is shown on the button, but may be truncated.
The DESCRIPTION is a tooltip that appears when the mouse hovers over the button.
When recording history, COMET initially uses the full command as the description; 
This allows the user to see the full text of the command, if it is truncated.
Optionally, a textfile can be edited (or created from scratch), where the desciption
is an actual description of the command instead of the full, untruncated text.

The only requirement of the textfile is having a single command on each line:

<COMMAND>
<COMMAND>
<COMMAND>
<COMMAND>

Adding a space between commands will toggle the color of the buttons until the next space is encountered

<COMMAND>	-gray
<COMMAND>	-gray

<COMMAND>	-darkgray
<COMMAND>	-darkgray

<COMMAND>	-gray
<COMMAND>	-gray

Finally, the history pane supports dropping text files to auto-load the commands.

Example text file, with mixed commands and descriptions:



sdboot						sdboot
sbl							stay in bootloader
qdver						qdver
ulcmd						ulcmd
sinvmode					sinvmode
qdver
qdeeinfo					get all info
rbt	rbt
rma
qdin,4,1
qdin,6,1


