<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="482dc5af-c67f-45ce-b074-d11af046bdfc" name="shell-script">
  <nodes>
    <node id="61e3300e-e41d-48e6-bea9-4a8ec828d897" concept="TextGen.Generator" name="ShellScript">
      <properties>
        <name>ShellScript</name>
      </properties>
      <children role="mappings">
        <node id="88c82d9a-4dd6-4108-97de-642fc2f4a37e" concept="TextGen.Mapping" name="Pipeline.Pipeline">
          <properties>
            <conceptKey>Pipeline.Pipeline</conceptKey>
          </properties>
          <children role="template">
            <node id="b4f3bd21-e2a6-4fde-865a-574421a091b5" concept="TextGen.Template">
              <children role="parts">
                <node id="f7fcebb0-b87d-4219-bdc1-ea0376bbec8a" concept="TextGen.Text" name="# ">
                  <properties>
                    <text># </text>
                  </properties>
                </node>
                <node id="58bfd14d-ee6b-429b-96c6-75ec3dcbf395" concept="TextGen.Property" name="name">
                  <properties>
                    <conceptKey>name</conceptKey>
                  </properties>
                </node>
                <node id="fadbcf2c-9391-49a6-b316-b1dc1d803db2" concept="TextGen.Line" />
                <node id="7601198f-279d-40f4-8890-3cc97053d0ac" concept="TextGen.Children" name="stages">
                  <properties>
                    <conceptKey>stages</conceptKey>
                  </properties>
                  <children role="separator">
                    <node id="0a146c58-ee03-43ad-a514-623db9d50a1c" concept="TextGen.Template">
                      <children role="parts">
                        <node id="51d25804-b9f3-4657-af5a-efb4724451b2" concept="TextGen.Line" />
                      </children>
                    </node>
                  </children>
                </node>
              </children>
            </node>
          </children>
        </node>
        <node id="d3cd2fa7-c52c-4a25-b864-1010323763ac" concept="TextGen.Mapping" name="Pipeline.Stage">
          <properties>
            <conceptKey>Pipeline.Stage</conceptKey>
          </properties>
          <children role="template">
            <node id="da37b4ac-baed-45ac-97c6-9a0112e5d119" concept="TextGen.Template">
              <children role="parts">
                <node id="168f8e13-6d8a-40ba-9a18-102ccd475fc2" concept="TextGen.WhenFilled" name="command">
                  <properties>
                    <conceptKey>command</conceptKey>
                  </properties>
                  <children role="body">
                    <node id="06db3a2f-640e-4095-8298-b54fae078ae9" concept="TextGen.Template">
                      <children role="parts">
                        <node id="ce31e42a-6cf3-4f97-a976-49e4f0719461" concept="TextGen.Property" name="command">
                          <properties>
                            <conceptKey>command</conceptKey>
                          </properties>
                        </node>
                      </children>
                    </node>
                  </children>
                  <children role="otherwise">
                    <node id="2642fa5d-1fb2-4787-beb0-2938d3c18b10" concept="TextGen.Template">
                      <children role="parts">
                        <node id="c10943aa-27e5-4e36-8ffa-b69ff1c76ca3" concept="TextGen.Text" name="# ">
                          <properties>
                            <text># </text>
                          </properties>
                        </node>
                        <node id="f9977f39-f4f3-464b-b770-0018f7aa056c" concept="TextGen.Property" name="name">
                          <properties>
                            <conceptKey>name</conceptKey>
                          </properties>
                        </node>
                        <node id="a41b0d49-f1b1-4938-ab9a-aed5e9f05b9f" concept="TextGen.Text" name=" has no command">
                          <properties>
                            <text> has no command</text>
                          </properties>
                        </node>
                      </children>
                    </node>
                  </children>
                </node>
              </children>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>