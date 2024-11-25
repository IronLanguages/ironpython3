from tkinter import *

class MultiStatusBar(Frame):

    def __init__(self, master=None, **kw):
        if master is None:
            master = Tk()
        Frame.__init__(self, master, **kw)
        self.labels = {}

    def set_label(self, name, text='', side=LEFT, width=0):
        if name not in self.labels:
            label = Label(self, borderwidth=0, anchor=W)
            label.pack(side=side, pady=0, padx=4)
            self.labels[name] = label
        else:
            label = self.labels[name]
        if width != 0:
            label.config(width=width)
        label.config(text=text)

def _multistatus_bar(parent):
    root = Tk()
    width, height, x, y = list(map(int, re.split('[x+]', parent.geometry())))
    root.geometry("+%d+%d" %(x, y + 150))
    root.title("Test multistatus bar")
    frame = Frame(root)
    text = Text(frame)
    text.pack()
    msb = MultiStatusBar(frame)
    msb.set_label("one", "hello")
    msb.set_label("two", "world")
    msb.pack(side=BOTTOM, fill=X)

    def change():
        msb.set_label("one", "foo")
        msb.set_label("two", "bar")

    button = Button(root, text="Update status", command=change)
    button.pack(side=BOTTOM)
    frame.pack()
    frame.mainloop()
    root.mainloop()

if __name__ == '__main__':
    from idlelib.idle_test.htest import run
    run(_multistatus_bar)
