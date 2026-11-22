<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b5000000-0000-4000-9007-000000000001" name="places">
  <nodes>
    <node id="b5000000-0000-4000-8007-000000000001" concept="Import.Importer" name="places">
      <properties>
        <name>places</name>
      </properties>
      <children role="tokens">
        <node id="b5000000-0000-4000-8007-000000000101" concept="Import.Skipped" name="Space">
          <properties>
            <tokenName>Space</tokenName>
            <prefix></prefix>
            <until>\n</until>
          </properties>
        </node>
        <node id="b5000000-0000-4000-8007-000000000102" concept="Import.Word" name="Word">
          <properties>
            <tokenName>Word</tokenName>
          </properties>
        </node>
      </children>
      <children role="rules">
        <node id="b5000000-0000-4000-8007-000000000201" concept="Import.Rule" name="Atlas.Map">
          <properties>
            <conceptKey>Atlas.Map</conceptKey>
          </properties>
          <children role="parts">
            <node id="b5000000-0000-4000-8007-000000000301" concept="Import.Literal" name="map">
              <properties>
                <text>map</text>
              </properties>
            </node>
            <node id="b5000000-0000-4000-8007-000000000401" concept="Import.Capture" name="name">
              <properties>
                <slot>name</slot>
                <token>Word</token>
              </properties>
            </node>
            <node id="b5000000-0000-4000-8007-000000000501" concept="Import.Repeat" name="places">
              <properties>
                <slot>places</slot>
                <separator></separator>
                <minimum>0</minimum>
              </properties>
            </node>
          </children>
        </node>
        <node id="b5000000-0000-4000-8007-000000000202" concept="Import.Rule" name="Atlas.Place">
          <properties>
            <conceptKey>Atlas.Place</conceptKey>
          </properties>
          <children role="parts">
            <node id="b5000000-0000-4000-8007-000000000302" concept="Import.Literal" name="place">
              <properties>
                <text>place</text>
              </properties>
            </node>
            <node id="b5000000-0000-4000-8007-000000000402" concept="Import.Capture" name="name">
              <properties>
                <slot>name</slot>
                <token>Word</token>
              </properties>
            </node>
            <node id="b5000000-0000-4000-8007-000000000502" concept="Import.Literal" name="at">
              <properties>
                <text>at</text>
              </properties>
            </node>
            <node id="b5000000-0000-4000-8007-000000000602" concept="Import.Capture" name="at">
              <properties>
                <slot>at</slot>
                <token>Word</token>
              </properties>
            </node>
          </children>
        </node>
        <node id="b5000000-0000-4000-8007-000000000203" concept="Import.Rule" name="Atlas.Landmark">
          <properties>
            <conceptKey>Atlas.Landmark</conceptKey>
          </properties>
          <children role="parts">
            <node id="b5000000-0000-4000-8007-000000000303" concept="Import.Literal" name="landmark">
              <properties>
                <text>landmark</text>
              </properties>
            </node>
            <node id="b5000000-0000-4000-8007-000000000403" concept="Import.Capture" name="name">
              <properties>
                <slot>name</slot>
                <token>Word</token>
              </properties>
            </node>
            <node id="b5000000-0000-4000-8007-000000000503" concept="Import.Literal" name="at">
              <properties>
                <text>at</text>
              </properties>
            </node>
            <node id="b5000000-0000-4000-8007-000000000603" concept="Import.Capture" name="at">
              <properties>
                <slot>at</slot>
                <token>Word</token>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>