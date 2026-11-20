<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="af67f5b5-9520-4a58-ad11-bd529cd76833" name="reads-relaxed">
  <nodes>
    <node id="b1e970d4-7d10-414a-a07f-630ed2f0ed2d" concept="Testing.Test" name="relaxed.ini">
      <properties>
        <document>relaxed.ini</document>
      </properties>
      <children role="assertion">
        <node id="b343adde-dd54-422d-a01a-8fd2078572c9" concept="Testing.Reads" name="relaxed">
          <properties>
            <importer>relaxed</importer>
            <generator>ShellScript</generator>
          </properties>
          <children role="lines">
            <node id="b96a6d4f-b495-4320-a122-b4ce2d7169b4" concept="Testing.Line" name="# Release">
              <properties>
                <text># Release</text>
              </properties>
            </node>
            <node id="e193bac9-fa6e-4233-a730-b3374f488f51" concept="Testing.Line" name="dotnet build">
              <properties>
                <text>dotnet build</text>
              </properties>
            </node>
            <node id="ece1e682-b600-476e-ab34-4fec81c61b84" concept="Testing.Line" name="dotnet test">
              <properties>
                <text>dotnet test</text>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>