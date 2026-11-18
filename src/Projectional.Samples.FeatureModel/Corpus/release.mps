<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="017a1f2b-f861-4775-b3c7-95c663a973ef" name="Release">
  <nodes>
    <node id="8b08c9bb-202d-418e-8e8b-789f5b8ca680" concept="Fm.Model" name="Release">
      <properties>
        <name>Release</name>
      </properties>
      <children role="features">
        <node id="c9ed2bd0-9cad-47fe-b336-c2639903a6c3" concept="Fm.Feature" name="Core">
          <properties>
            <name>Core</name>
            <isSelected>true</isSelected>
          </properties>
        </node>
        <node id="1ed328c3-b8a6-458b-88bc-ff9b775b2b56" concept="Fm.Feature" name="Ui">
          <properties>
            <name>Ui</name>
            <isSelected>true</isSelected>
          </properties>
        </node>
        <node id="65af3a1e-06f2-4bc2-9c55-5931e41518fa" concept="Fm.Feature" name="Safe">
          <properties>
            <name>Safe</name>
            <isSelected>true</isSelected>
          </properties>
        </node>
        <node id="ec14e72c-b19a-4465-858d-93ef12554aa4" concept="Fm.Feature" name="Experimental">
          <properties>
            <name>Experimental</name>
            <isSelected>false</isSelected>
          </properties>
        </node>
      </children>
      <children role="constraints">
        <node id="1f9564d2-806a-43c8-80e3-820079be90fb" concept="Fm.Requires">
          <references role="subject" target="c9ed2bd0-9cad-47fe-b336-c2639903a6c3" resolveInfo="Core" />
          <references role="other" target="65af3a1e-06f2-4bc2-9c55-5931e41518fa" resolveInfo="Safe" />
        </node>
        <node id="12c78e62-8576-4096-ba98-59a39bb56615" concept="Fm.Requires">
          <references role="subject" target="1ed328c3-b8a6-458b-88bc-ff9b775b2b56" resolveInfo="Ui" />
          <references role="other" target="c9ed2bd0-9cad-47fe-b336-c2639903a6c3" resolveInfo="Core" />
        </node>
        <node id="b948413a-58be-49be-bffd-c90d511b107f" concept="Fm.Excludes">
          <references role="subject" target="65af3a1e-06f2-4bc2-9c55-5931e41518fa" resolveInfo="Safe" />
          <references role="other" target="ec14e72c-b19a-4465-858d-93ef12554aa4" resolveInfo="Experimental" />
        </node>
      </children>
    </node>
  </nodes>
</model>