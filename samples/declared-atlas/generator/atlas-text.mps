<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b4000000-0000-4000-9006-000000000001" name="atlas-text">
  <nodes>
    <node id="b4000000-0000-4000-8006-000000000001" concept="TextGen.Generator" name="AtlasText">
      <properties>
        <name>AtlasText</name>
      </properties>
      <children role="mappings">
        <node id="b4000000-0000-4000-8006-000000000002" concept="TextGen.Mapping" name="Atlas.Map">
          <properties>
            <conceptKey>Atlas.Map</conceptKey>
          </properties>
          <children role="template">
            <node id="b4000000-0000-4000-8006-000000000102" concept="TextGen.Template">
              <children role="parts">
                <node id="b4000000-0000-4000-8006-000000000202" concept="TextGen.Text" name="map ">
                  <properties>
                    <text>map </text>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000000302" concept="TextGen.Property" name="name">
                  <properties>
                    <conceptKey>name</conceptKey>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000001402" concept="TextGen.WhenFilled" name="entry">
                  <properties>
                    <conceptKey>entry</conceptKey>
                  </properties>
                  <children role="body">
                    <node id="b4000000-0000-4000-8006-000000001502" concept="TextGen.Template">
                      <children role="parts">
                        <node id="b4000000-0000-4000-8006-000000001602" concept="TextGen.Text" name=" at ">
                          <properties>
                            <text> at </text>
                          </properties>
                        </node>
                        <node id="b4000000-0000-4000-8006-000000001702" concept="TextGen.Reference" name="entry">
                          <properties>
                            <conceptKey>entry</conceptKey>
                            <unsetText>&lt;none&gt;</unsetText>
                            <unresolvedText>&lt;unresolved&gt;</unresolvedText>
                          </properties>
                        </node>
                      </children>
                    </node>
                  </children>
                  <children role="otherwise">
                    <node id="b4000000-0000-4000-8006-000000003102" concept="TextGen.Template" />
                  </children>
                </node>
                <node id="b4000000-0000-4000-8006-000000000602" concept="TextGen.Line" />
                <node id="b4000000-0000-4000-8006-000000000702" concept="TextGen.Children" name="places">
                  <properties>
                    <conceptKey>places</conceptKey>
                  </properties>
                  <children role="separator">
                    <node id="b4000000-0000-4000-8006-000000000802" concept="TextGen.Template">
                      <children role="parts">
                        <node id="b4000000-0000-4000-8006-000000000902" concept="TextGen.Line" />
                      </children>
                    </node>
                  </children>
                </node>
                <node id="b4000000-0000-4000-8006-000000001002" concept="TextGen.Line" />
                <node id="b4000000-0000-4000-8006-000000001102" concept="TextGen.Children" name="routes">
                  <properties>
                    <conceptKey>routes</conceptKey>
                  </properties>
                  <children role="separator">
                    <node id="b4000000-0000-4000-8006-000000001202" concept="TextGen.Template">
                      <children role="parts">
                        <node id="b4000000-0000-4000-8006-000000001302" concept="TextGen.Line" />
                      </children>
                    </node>
                  </children>
                </node>
              </children>
            </node>
          </children>
        </node>
        <node id="b4000000-0000-4000-8006-000000000003" concept="TextGen.Mapping" name="Atlas.Place">
          <properties>
            <conceptKey>Atlas.Place</conceptKey>
          </properties>
          <children role="template">
            <node id="b4000000-0000-4000-8006-000000000103" concept="TextGen.Template">
              <children role="parts">
                <node id="b4000000-0000-4000-8006-000000000203" concept="TextGen.Text" name="place ">
                  <properties>
                    <text>place </text>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000000303" concept="TextGen.Property" name="name">
                  <properties>
                    <conceptKey>name</conceptKey>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000000403" concept="TextGen.Text" name=" at ">
                  <properties>
                    <text> at </text>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000000503" concept="TextGen.Property" name="at">
                  <properties>
                    <conceptKey>at</conceptKey>
                  </properties>
                </node>
              </children>
            </node>
          </children>
        </node>
        <node id="b4000000-0000-4000-8006-000000000004" concept="TextGen.Mapping" name="Atlas.Landmark">
          <properties>
            <conceptKey>Atlas.Landmark</conceptKey>
          </properties>
          <children role="template">
            <node id="b4000000-0000-4000-8006-000000000104" concept="TextGen.Template">
              <children role="parts">
                <node id="b4000000-0000-4000-8006-000000000204" concept="TextGen.Text" name="landmark ">
                  <properties>
                    <text>landmark </text>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000000304" concept="TextGen.Property" name="name">
                  <properties>
                    <conceptKey>name</conceptKey>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000000404" concept="TextGen.Text" name=" at ">
                  <properties>
                    <text> at </text>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000000504" concept="TextGen.Property" name="at">
                  <properties>
                    <conceptKey>at</conceptKey>
                  </properties>
                </node>
              </children>
            </node>
          </children>
        </node>
        <node id="b4000000-0000-4000-8006-000000000005" concept="TextGen.Mapping" name="Atlas.Route">
          <properties>
            <conceptKey>Atlas.Route</conceptKey>
          </properties>
          <children role="template">
            <node id="b4000000-0000-4000-8006-000000000105" concept="TextGen.Template">
              <children role="parts">
                <node id="b4000000-0000-4000-8006-000000000205" concept="TextGen.Text" name="route ">
                  <properties>
                    <text>route </text>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000000305" concept="TextGen.Property" name="name">
                  <properties>
                    <conceptKey>name</conceptKey>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000001605" concept="TextGen.WhenFilled" name="onMap">
                  <properties>
                    <conceptKey>onMap</conceptKey>
                  </properties>
                  <children role="body">
                    <node id="b4000000-0000-4000-8006-000000001705" concept="TextGen.Template">
                      <children role="parts">
                        <node id="b4000000-0000-4000-8006-000000003105" concept="TextGen.Text" name=" on ">
                          <properties>
                            <text> on </text>
                          </properties>
                        </node>
                        <node id="b4000000-0000-4000-8006-000000001905" concept="TextGen.Reference" name="onMap">
                          <properties>
                            <conceptKey>onMap</conceptKey>
                            <unsetText>&lt;none&gt;</unsetText>
                            <unresolvedText>&lt;unresolved&gt;</unresolvedText>
                          </properties>
                        </node>
                      </children>
                    </node>
                  </children>
                  <children role="otherwise">
                    <node id="b4000000-0000-4000-8006-000000004405" concept="TextGen.Template" />
                  </children>
                </node>
                <node id="b4000000-0000-4000-8006-000000000605" concept="TextGen.Line" />
                <node id="b4000000-0000-4000-8006-000000000705" concept="TextGen.Children" name="stops">
                  <properties>
                    <conceptKey>stops</conceptKey>
                  </properties>
                  <children role="separator">
                    <node id="b4000000-0000-4000-8006-000000000805" concept="TextGen.Template">
                      <children role="parts">
                        <node id="b4000000-0000-4000-8006-000000000905" concept="TextGen.Line" />
                      </children>
                    </node>
                  </children>
                </node>
              </children>
            </node>
          </children>
        </node>
        <node id="b4000000-0000-4000-8006-000000000006" concept="TextGen.Mapping" name="Atlas.Stop">
          <properties>
            <conceptKey>Atlas.Stop</conceptKey>
          </properties>
          <children role="template">
            <node id="b4000000-0000-4000-8006-000000000106" concept="TextGen.Template">
              <children role="parts">
                <node id="b4000000-0000-4000-8006-000000000206" concept="TextGen.Text" name="to ">
                  <properties>
                    <text>to </text>
                  </properties>
                </node>
                <node id="b4000000-0000-4000-8006-000000000306" concept="TextGen.Reference" name="to">
                  <properties>
                    <conceptKey>to</conceptKey>
                    <unsetText>&lt;none&gt;</unsetText>
                    <unresolvedText>&lt;unresolved&gt;</unresolvedText>
                  </properties>
                </node>
              </children>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>