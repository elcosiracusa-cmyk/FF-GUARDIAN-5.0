using System.Drawing.Drawing2D;

namespace FFGuardian;

internal sealed class MainForm : Form
{
    private static readonly Color Bg=Color.FromArgb(4,10,18), Surface=Color.FromArgb(8,22,38), Surface2=Color.FromArgb(11,31,52), Blue=Color.FromArgb(0,125,255), Cyan=Color.FromArgb(0,215,255), Green=Color.FromArgb(66,226,118), Orange=Color.FromArgb(255,166,45), Red=Color.FromArgb(255,70,82);
    private readonly DefenderService _defender=new();
    private readonly Panel _content=new(){Dock=DockStyle.Fill,BackColor=Bg};
    private readonly Label _status=new(){Dock=DockStyle.Bottom,Height=30,ForeColor=Color.Silver,BackColor=Color.FromArgb(5,15,26),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(15,0,0,0)};
    private readonly List<Button> _nav=[];

    public MainForm()
    {
        Text="FF GUARDIAN 5.0 Definitive — Next Gen Security Suite by EL.CO";
        WindowState=FormWindowState.Maximized; MinimumSize=new Size(1280,780); BackColor=Bg; ForeColor=Color.White; Font=new Font("Segoe UI",10); DoubleBuffered=true;
        Controls.Add(_content); Controls.Add(BuildSidebar()); Controls.Add(BuildHeader()); Controls.Add(_status);
        Shown+=async(_,_)=>await SafeAsync(ShowDashboardAsync);
    }

    private Control BuildHeader()
    {
        var p=new Panel{Dock=DockStyle.Top,Height=78,BackColor=Color.FromArgb(5,15,27),Padding=new Padding(305,0,18,0)};
        p.Controls.Add(new Label{Text="FF GUARDIAN 5.0 DEFINITIVE",ForeColor=Color.White,Font=new Font("Segoe UI",21,FontStyle.Bold),Dock=DockStyle.Left,Width=520,TextAlign=ContentAlignment.MiddleLeft});
        var emergency=Button("⚠  MODALITÀ EMERGENZA",225,44,Red); emergency.Dock=DockStyle.Right; emergency.Click+=async(_,_)=>await EmergencyAsync();
        var refresh=Button("⟳  AGGIORNA SISTEMA",195,44,Blue); refresh.Dock=DockStyle.Right; refresh.Click+=async(_,_)=>await SafeAsync(ShowDashboardAsync);
        p.Controls.Add(emergency); p.Controls.Add(refresh); return p;
    }

    private Control BuildSidebar()
    {
        var p=new Panel{Dock=DockStyle.Left,Width=290,BackColor=Color.FromArgb(5,17,29),Padding=new Padding(14)};
        var brand=new Panel{Dock=DockStyle.Top,Height=215}; brand.Paint+=(_,e)=>PaintBrand(e.Graphics); p.Controls.Add(brand);
        var menu=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(0,8,0,0)};
        AddNav(menu,"⌂  Dashboard",ShowDashboardAsync); AddNav(menu,"⌕  Scansioni",()=>{ShowScans();return Task.CompletedTask;}); AddNav(menu,"⚠  Minacce rilevate",ShowThreatsAsync); AddNav(menu,"◈  Centro Protezione",ShowProtectionAsync); AddNav(menu,"▣  Quarantena",()=>{ShowQuarantine();return Task.CompletedTask;}); AddNav(menu,"⚙  Strumenti sistema",()=>{ShowTools();return Task.CompletedTask;}); AddNav(menu,"≡  Report e registro",ShowLogsAsync); AddNav(menu,"●  Informazioni",()=>{ShowInfo();return Task.CompletedTask;});
        p.Controls.Add(menu); return p;
    }

    private static void PaintBrand(Graphics g)
    {
        g.SmoothingMode=SmoothingMode.AntiAlias; using var pen=new Pen(Cyan,4); var pts=new[]{new Point(145,10),new Point(224,46),new Point(210,137),new Point(145,184),new Point(80,137),new Point(66,46)}; g.DrawPolygon(pen,pts);
        using var dog=new Font("Segoe UI Symbol",54,FontStyle.Bold); g.DrawString("♞",dog,Brushes.White,94,35); using var eye=new SolidBrush(Orange); g.FillEllipse(eye,118,78,9,7); g.FillEllipse(eye,163,78,9,7);
        using var title=new Font("Segoe UI",19,FontStyle.Bold); g.DrawString("FFGuardian",title,Brushes.White,62,143); using var sub=new Font("Segoe UI",11,FontStyle.Bold); g.DrawString("BY EL.CO",sub,Brushes.DeepSkyBlue,111,177);
    }

    private void AddNav(Control parent,string text,Func<Task> action)
    {
        var b=Button(text,250,50,Surface2); b.Margin=new Padding(0,3,0,3); b.TextAlign=ContentAlignment.MiddleLeft; b.Padding=new Padding(15,0,0,0);
        b.Click+=async(_,_)=>{_nav.ForEach(x=>x.BackColor=Surface2);b.BackColor=Blue;await SafeAsync(action);}; _nav.Add(b);parent.Controls.Add(b);
    }

    private async Task ShowDashboardAsync()
    {
        Clear("Dashboard di Protezione","Panoramica completa dello stato di sicurezza del sistema"); _status.Text="Lettura dello stato Microsoft Defender..."; var s=await _defender.GetStateAsync();
        var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=3,Padding=new Padding(18)}; grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,37));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));grid.RowStyles.Add(new RowStyle(SizeType.Percent,44));grid.RowStyles.Add(new RowStyle(SizeType.Percent,25));grid.RowStyles.Add(new RowStyle(SizeType.Percent,31));
        grid.Controls.Add(ScoreCard(s),0,0);grid.Controls.Add(QuickCard(),1,0);grid.Controls.Add(BrandCard(s),2,0);var cards=ProtectionCards(s);grid.Controls.Add(cards,0,1);grid.SetColumnSpan(cards,3);grid.Controls.Add(ActivityCard(s),0,2);grid.Controls.Add(AdviceCard(s),1,2);grid.Controls.Add(InfoCard(s),2,2);_content.Controls.Add(grid);grid.BringToFront();_status.Text=$"Sistema aggiornato alle {DateTime.Now:HH:mm:ss} — Definizioni {s.SignatureVersion}";
    }

    private Control ScoreCard(SecurityState s)
    {
        var p=Card("PUNTEGGIO DI PROTEZIONE"); p.Paint+=(_,e)=>{e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(44,65,205,205);using var basePen=new Pen(Color.FromArgb(35,70,95),18);using var scorePen=new Pen(s.Score>=85?Green:s.Score>=65?Orange:Red,18){StartCap=LineCap.Round,EndCap=LineCap.Round};e.Graphics.DrawArc(basePen,r,135,270);e.Graphics.DrawArc(scorePen,r,135,270*s.Score/100f);using var f=new Font("Segoe UI",43,FontStyle.Bold);var t=s.Score.ToString();var z=e.Graphics.MeasureString(t,f);e.Graphics.DrawString(t,f,Brushes.White,146-z.Width/2,126);using var sm=new Font("Segoe UI",14,FontStyle.Bold);e.Graphics.DrawString("/100",sm,Brushes.Silver,126,185);e.Graphics.DrawString(s.Score>=85?"PROTEZIONE ELEVATA":s.Score>=65?"DA MIGLIORARE":"INTERVENTO NECESSARIO",sm,new SolidBrush(s.Score>=85?Green:s.Score>=65?Orange:Red),45,284);}; return p;
    }

    private Control QuickCard()
    {
        var p=Card("AZIONI RAPIDE");var g=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(12)};g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));g.RowStyles.Add(new RowStyle(SizeType.Percent,50));g.RowStyles.Add(new RowStyle(SizeType.Percent,50));g.Controls.Add(Action("⚡\nScansione rapida",()=>RunAsync(_defender.QuickScanAsync,"Scansione rapida avviata.")),0,0);g.Controls.Add(Action("◉\nScansione completa",()=>RunAsync(_defender.FullScanAsync,"Scansione completa avviata.")),1,0);g.Controls.Add(Action("▣\nScansiona cartella",FolderScanAsync),0,1);g.Controls.Add(Action("⟳\nAggiorna definizioni",()=>RunAsync(_defender.UpdateAsync,"Definizioni aggiornate.")),1,1);p.Controls.Add(g);return p;
    }

    private static Control BrandCard(SecurityState s){var p=Card("NEXT GEN SECURITY");p.Controls.Add(new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",17,FontStyle.Bold),ForeColor=Cyan,Text=$"♞\nFFGuardian\nBy EL.CO\n\n{(s.Score>=85?"SISTEMA PROTETTO":"CONTROLLO RICHIESTO")}\nSicurezza • Controllo • Protezione"});return p;}

    private static Control ProtectionCards(SecurityState s)
    {
        var f=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(2),WrapContents=false,AutoScroll=true};var data=new[]{("Microsoft Defender",s.Antivirus),("Tempo reale",s.Realtime),("Firewall",s.Firewall),("Definizioni",s.Signatures),("Protezione PUA",s.Pua),("Protezione rete",s.Network),("Ransomware Guard",s.Ransomware)};
        foreach(var x in data){var p=new Panel{Width=190,Height=110,Margin=new Padding(6),BackColor=Surface,Padding=new Padding(12)};p.Controls.Add(new Label{Dock=DockStyle.Top,Height=27,Text=x.Item1,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.White});p.Controls.Add(new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Text=x.Item2?"✓\nATTIVO":"!\nDA CONFIGURARE",Font=new Font("Segoe UI",12,FontStyle.Bold),ForeColor=x.Item2?Green:Orange});f.Controls.Add(p);}return f;
    }

    private static Control ActivityCard(SecurityState s){var p=Card("ATTIVITÀ RECENTI");p.Controls.Add(new Label{Dock=DockStyle.Fill,Padding=new Padding(16),ForeColor=Color.Gainsboro,Text=$"✓ Stato Defender verificato\n\n✓ Definizioni: {s.SignatureVersion}\n\n✓ Ultima rapida: {s.LastQuickScan}\n\n✓ Ultima completa: {s.LastFullScan}"});return p;}
    private static Control AdviceCard(SecurityState s){var p=Card("AZIONI CONSIGLIATE");p.Controls.Add(new Label{Dock=DockStyle.Fill,Padding=new Padding(16),ForeColor=s.Issues.Count==0?Green:Orange,Text=s.Issues.Count==0?"✓ Nessun intervento urgente. Il sistema è protetto.":string.Join("\n\n",s.Issues.Select(x=>"• "+x))});return p;}
    private static Control InfoCard(SecurityState s){var p=Card("INFORMAZIONI SISTEMA");p.Controls.Add(new Label{Dock=DockStyle.Fill,Padding=new Padding(15),ForeColor=Color.Gainsboro,Text=$"Computer: {Environment.MachineName}\nUtente: {Environment.UserName}\nWindows: {Environment.OSVersion}\n.NET: {Environment.Version}\n\nMotore: {s.EngineVersion}\nDefinizioni: {s.SignatureVersion}"});return p;}

    private void ShowScans(){Clear("Centro Scansioni","Analisi Microsoft Defender e controlli personalizzati");var f=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(35),AutoScroll=true};f.Controls.Add(Action("⚡\nSCANSIONE RAPIDA",()=>RunAsync(_defender.QuickScanAsync,"Scansione rapida avviata."),320,150));f.Controls.Add(Action("◉\nSCANSIONE COMPLETA",()=>RunAsync(_defender.FullScanAsync,"Scansione completa avviata."),320,150));f.Controls.Add(Action("▣\nCARTELLA PERSONALIZZATA",FolderScanAsync,320,150));f.Controls.Add(Action("⟳\nAGGIORNA DEFINIZIONI",()=>RunAsync(_defender.UpdateAsync,"Definizioni aggiornate."),320,150));f.Controls.Add(Action("◈\nSICUREZZA WINDOWS",()=>{_defender.OpenWindowsSecurity();return Task.CompletedTask;},320,150));_content.Controls.Add(f);f.BringToFront();}
    private async Task ShowThreatsAsync(){Clear("Minacce rilevate","Cronologia delle rilevazioni Microsoft Defender");var d=await _defender.GetThreatsAsync();var g=Grid();g.DataSource=d;_content.Controls.Add(g);g.BringToFront();_status.Text=$"{d.Count} rilevazioni caricate.";}
    private async Task ShowProtectionAsync(){Clear("Centro Protezione","Stato completo delle difese Windows");var s=await _defender.GetStateAsync();var g=Grid();g.DataSource=new[]{new{Componente="Microsoft Defender",Stato=s.Antivirus?"Attivo":"Disattivato"},new{Componente="Protezione tempo reale",Stato=s.Realtime?"Attiva":"Disattivata"},new{Componente="Definizioni",Stato=s.Signatures?"Aggiornate":"Da aggiornare"},new{Componente="Firewall",Stato=s.Firewall?"Attivo":"Da verificare"},new{Componente="Protezione PUA",Stato=s.Pua?"Blocco":"Non in blocco"},new{Componente="Protezione rete",Stato=s.Network?"Blocco":"Non in blocco"},new{Componente="Ransomware Guard",Stato=s.Ransomware?"Attivo":"Disattivato"}};_content.Controls.Add(g);g.BringToFront();}
    private void ShowQuarantine(){Clear("Quarantena","Gestione sicura degli elementi isolati");var p=Card("QUARANTENA MICROSOFT DEFENDER");p.Dock=DockStyle.Fill;p.Controls.Add(new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",15),ForeColor=Color.Silver,Text="FF GUARDIAN utilizza la quarantena protetta di Microsoft Defender.\n\nApri la Cronologia protezione per visualizzare, ripristinare o eliminare gli elementi isolati."});var b=Button("APRI CRONOLOGIA PROTEZIONE",340,54,Blue);b.Dock=DockStyle.Bottom;b.Click+=(_,_)=>_defender.OpenWindowsSecurity();p.Controls.Add(b);_content.Controls.Add(p);p.BringToFront();}
    private void ShowTools(){Clear("Strumenti di Sistema","Diagnostica, riparazione e manutenzione Windows");var f=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(35),AutoScroll=true};f.Controls.Add(Action("SFC /SCANNOW",()=>ToolAsync("sfc.exe","/scannow"),300,120));f.Controls.Add(Action("DISM RESTOREHEALTH",()=>ToolAsync("dism.exe","/Online /Cleanup-Image /RestoreHealth"),300,120));f.Controls.Add(Action("WINDOWS UPDATE",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:windowsupdate"){UseShellExecute=true});return Task.CompletedTask;},300,120));f.Controls.Add(Action("SICUREZZA WINDOWS",()=>{_defender.OpenWindowsSecurity();return Task.CompletedTask;},300,120));f.Controls.Add(Action("GESTIONE ATTIVITÀ",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("taskmgr.exe"){UseShellExecute=true});return Task.CompletedTask;},300,120));_content.Controls.Add(f);f.BringToFront();}
    private async Task ShowLogsAsync(){Clear("Report e Registro","Eventi operativi recenti di Microsoft Defender");var d=await _defender.GetOperationalEventsAsync();var g=Grid();g.DataSource=d;_content.Controls.Add(g);g.BringToFront();}
    private void ShowInfo(){Clear("Informazioni","FF GUARDIAN 5.0 Definitive");_content.Controls.Add(new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Silver,Font=new Font("Segoe UI",15),Text="FF GUARDIAN 5.0 DEFINITIVE\nNext Gen Security Suite\nBy EL.CO di Francesco Fazzina\n\nConsole professionale per Microsoft Defender e sicurezza Windows.\nVersione 5.0.0"});}

    private async Task EmergencyAsync(){if(MessageBox.Show("Aggiornare le definizioni e avviare una scansione rapida?","Modalità emergenza",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes)await RunAsync(async()=>{await _defender.UpdateAsync();await _defender.QuickScanAsync();},"Modalità emergenza avviata.");}
    private async Task FolderScanAsync(){using var d=new FolderBrowserDialog{Description="Seleziona la cartella da analizzare"};if(d.ShowDialog(this)==DialogResult.OK)await RunAsync(()=>_defender.CustomScanAsync(d.SelectedPath),"Scansione cartella avviata.");}
    private async Task RunAsync(Func<Task>a,string ok){try{_status.Text="Operazione in corso...";await a();_status.Text=ok;MessageBox.Show(ok,"FF GUARDIAN",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){Error(ex);}}
    private async Task ToolAsync(string file,string args){await Task.Run(()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file,args){UseShellExecute=true,Verb="runas"}));_status.Text=$"Strumento avviato: {file}";}
    private async Task SafeAsync(Func<Task>a){try{await a();}catch(Exception ex){Error(ex);}}
    private void Error(Exception ex){_status.Text="Errore gestito";MessageBox.Show(ex.Message,"FF GUARDIAN — Operazione non riuscita",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    private void Clear(string title,string sub){_content.Controls.Clear();var h=new Panel{Dock=DockStyle.Top,Height=80,BackColor=Color.FromArgb(6,18,31),Padding=new Padding(22,10,0,0)};h.Controls.Add(new Label{Text=title,Dock=DockStyle.Top,Height=38,Font=new Font("Segoe UI",20,FontStyle.Bold),ForeColor=Color.White});h.Controls.Add(new Label{Text=sub,Dock=DockStyle.Bottom,Height=25,ForeColor=Color.Silver});_content.Controls.Add(h);h.BringToFront();}
    private static Panel Card(string title){var p=new Panel{Dock=DockStyle.Fill,Margin=new Padding(8),BackColor=Surface,Padding=new Padding(12)};p.Controls.Add(new Label{Text=title,Dock=DockStyle.Top,Height=35,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.White});return p;}
    private static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Bg,ForeColor=Color.White,RowHeadersVisible=false,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Blue,ForeColor=Color.White},DefaultCellStyle=new DataGridViewCellStyle{BackColor=Surface2,ForeColor=Color.White,SelectionBackColor=Color.FromArgb(0,80,150)}};
    private static Button Button(string text,int w,int h,Color c)=>new(){Text=text,Width=w,Height=h,BackColor=c,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold),Cursor=Cursors.Hand,FlatAppearance={BorderColor=Color.FromArgb(0,110,200),BorderSize=1}};
    private static Button Action(string text,Func<Task>a,int w=180,int h=100){var b=Button(text,w,h,Surface2);b.Margin=new Padding(8);b.Click+=async(_,_)=>await a();return b;}
}
