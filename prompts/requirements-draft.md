Ich arbeite mit Microsoft Navision (.NET 4.6). 
Ich habe einen Service (REST API), um eine PDF-Datei und eine XML-Datei in eine PDF-Datei im ZUGFeRD-Format zu konvertieren. 
In Navision kann ich eine PDF-Datei mit der Rechnung und eine XML-Datei mit den Rechnungsdaten erstellen und speichern. 
Ich kann den REST API Service nicht direkt aufrufen, weil Navision keine Stream-Bibliothek hat. 
Ich brauche eine DLL-Bibliothek. Ich werde in Navision zwei Dateien erstellen und im Temp-Ordner speichern. 
Dann rufe ich die Bibliothek auf (Parameter: URL, PDF-Dateipfad, XML-Dateipfad). 
Die Bibliothek erstellt einen POST-Request, erhält eine Antwort mit einer PDF-ZUGFeRD-Datei, speichert die Datei und gibt den Pfad zurück. 
Sieh dir openapi/convert.yaml an.