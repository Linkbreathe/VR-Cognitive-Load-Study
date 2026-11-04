"""Native folder picker, invoked only by the local operator's Browse button."""
import tkinter as tk
from tkinter import filedialog

root = tk.Tk()
root.withdraw()
root.attributes("-topmost", True)
folder = filedialog.askdirectory(title="Choose test data storage folder", parent=root)
root.destroy()
print(folder)
