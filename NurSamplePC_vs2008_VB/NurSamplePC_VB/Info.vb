Imports System.Collections.Generic
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows.Forms
Imports System.Collections
Imports NurApiDotNet

Public Partial Class Info
	Inherits UserControl
	Private hNur As NurApi = Nothing

	Public Sub New()
		InitializeComponent()
	End Sub

	''' <summary>
	''' Sets the NurApi.
	''' </summary>
	''' <param name="hNur">The handle of NurApi.</param>
	Public Sub SetNurApi(hNur As NurApi)
		Try
			Me.hNur = hNur
			tabPage1.Tag = hNur
			' Set event handlers for NurApi
			AddHandler hNur.DisconnectedEvent, New EventHandler(Of NurApi.NurEventArgs)(AddressOf hNur_DisconnectedEvent)
			AddHandler hNur.ConnectedEvent, New EventHandler(Of NurApi.NurEventArgs)(AddressOf hNur_ConnectedEvent)

			' Update the status of the connection
			If hNur.IsConnected() Then
				hNur_ConnectedEvent(hNur, Nothing)
			Else
				hNur_DisconnectedEvent(hNur, Nothing)
			End If
		Catch ex As NurApiException
			MessageBox.Show(ex.ToString(), Program.appName, MessageBoxButtons.OK, MessageBoxIcon.[Error])
		End Try
	End Sub

	''' <summary>
	''' Handles the DisconnectedEvent event of the NUR module.
	''' </summary>
	''' <param name="sender">The source of the event.</param>
	''' <param name="e">The <see cref="NurApi.NurEventArgs" /> instance containing the event data.</param>
	Private Sub hNur_DisconnectedEvent(sender As Object, e As NurApi.NurEventArgs)
		Dim hNur As NurApi = TryCast(sender, NurApi)
		UpdateInfo(hNur)
	End Sub

	''' <summary>
	''' Handles the ConnectedEvent event of the NUR module.
	''' </summary>
	''' <param name="sender">The source of the event.</param>
	''' <param name="e">The <see cref="NurApi.NurEventArgs" /> instance containing the event data.</param>
	Private Sub hNur_ConnectedEvent(sender As Object, e As NurApi.NurEventArgs)
		Dim hNur As NurApi = TryCast(sender, NurApi)
		UpdateInfo(hNur)
	End Sub

	''' <summary>
	''' Updates the info.
	''' </summary>
	''' <param name="hNur">The NUR module handler.</param>
	Private Sub UpdateInfo(hNur As NurApi)
		Dim outdated As Boolean = False

		treeView1.Nodes.Clear()

		Dim node As TreeNode
		Dim dllNode As TreeNode = treeView1.Nodes.Add("DLL Versions")

		Try
			dllNode.Nodes.Add("NurApi = " & hNur.GetFileVersion())
			dllNode.Nodes.Add("NurApiDotNet = " & NurUtils.NurApiDotNetVersion)
		Catch ex As NurApiException
			AddExceptionNode(dllNode, ex, True)
		End Try

		If hNur.IsConnected() Then
			Dim fwinfoNode As TreeNode = treeView1.Nodes.Add("FWINFO")
			Dim structsNode As TreeNode = treeView1.Nodes.Add("Module settings")

			Try
				Dim readerInfo As NurApi.ReaderInfo = hNur.GetReaderInfo()
				tabPage1.Text = readerInfo.name & " - " & readerInfo.serial & " - " & readerInfo.GetVersionString()
				DumpObject(structsNode, readerInfo, Nothing)
			Catch ex As NurApiException
				AddExceptionNode(structsNode, ex, False)
			End Try

			Try
					'DumpObject(structsNode, hNur.GetVersions(), null);
				DumpObject(structsNode, GetVersions(hNur), Nothing)
			Catch ex As NurApiException
				AddExceptionNode(structsNode, ex, False)
			End Try

			Try
                Dim fwinfo As New NurApiDotNet.NurFwInfoParser(hNur.GetFWINFO())
				For Each entry As KeyValuePair(Of String, String) In fwinfo.keypairs
					fwinfoNode.Nodes.Add(entry.Key & " = " & entry.Value)
				Next
			Catch ex As NurApiException
				AddExceptionNode(fwinfoNode, ex, False)
				fwinfoNode.Expand()
				outdated = True
			End Try

			Try
				DumpObject(structsNode, hNur.GetDeviceCaps(), Nothing)
			Catch ex As NurApiException
				AddExceptionNode(structsNode, ex, False)
			End Try

			Try
				DumpObject(structsNode, hNur.GetEthConfig(), Nothing)
			Catch ex As NurApiException
				AddExceptionNode(structsNode, ex, False)
			End Try

			Try
				DumpObject(structsNode, hNur.GetModuleSetup(), Nothing)
			Catch ex As NurApiException
				AddExceptionNode(structsNode, ex, False)
			End Try

			Try
				DumpObject(structsNode, hNur.GetSensorConfig(), Nothing)
			Catch ex As NurApiException
				AddExceptionNode(structsNode, ex, False)
			End Try

			Dim regNode As TreeNode = treeView1.Nodes.Add("Regions")
			Dim numRegions As Integer = 0
			Try
				Dim readerInfo As NurApi.ReaderInfo = hNur.GetReaderInfo()
				' Dump region infos
				For i As Integer = 0 To readerInfo.numRegions - 1
					Dim ri As NurApi.RegionInfo = hNur.GetRegionInfo(i)
					DumpObject(regNode, ri, String.Format("Region {0}", i))
					numRegions += 1
				Next
				' Dump CustomHoptable
				Try
					Dim customHoptableEx As NurApi.CustomHoptableEx = hNur.GetCustomHoptableEx()
					DumpObject(regNode, customHoptableEx, String.Format("Region {0}", NurApi.REGIONID_CUSTOM))
					numRegions += 1
				Catch ex As NurApiException
					Try
						AddExceptionNode(regNode, ex, False)
						Dim customHoptable As NurApi.CustomHoptable = hNur.GetCustomHoptable()
						DumpObject(regNode, customHoptable, String.Format("Region {0}", NurApi.REGIONID_CUSTOM))
						numRegions += 1
					Catch ex2 As NurApiException
						AddExceptionNode(regNode, ex2, False)
					End Try
				End Try
			Catch ex As NurApiException
				AddExceptionNode(structsNode, ex, True)
			End Try
			regNode.Text = String.Format("{0} {1}", regNode.Text, numRegions)

			Dim antNode As TreeNode = treeView1.Nodes.Add("Antennas")
			Try
				Dim moduleSetup As NurApi.ModuleSetup = hNur.GetModuleSetup()
				antNode.Nodes.Add("Selected = " & (If(moduleSetup.selectedAntenna = -1, "Auto", moduleSetup.selectedAntenna.ToString())))
				If moduleSetup.antennaMask <> 0 Then
					antNode.Nodes.Add("Enabled = " & GetEnabledAntennas(moduleSetup.antennaMask))
					node = antNode.Nodes.Add("Reflected Powers")
					If MeasureReflectedPowers(hNur, node) Then
						antNode.Expand()
					End If
				Else
					node = antNode.Nodes.Add("No Enabled Antennas!!!")
					node.ForeColor = System.Drawing.Color.Red
					node.Expand()
				End If
			Catch generatedExceptionName As NurApiException
				node = antNode.Nodes.Add("Can't get antenna settings")
				node.ForeColor = System.Drawing.Color.Red
				node.Expand()
			End Try
			structsNode.Expand()
		Else
			tabPage1.Text = "No Connection"
		End If

		If outdated Then
			node = treeView1.Nodes.Add("The NUR modules firmware might be outdated. Please check for updates.")
			node.ForeColor = System.Drawing.Color.Red
			node.Nodes.Add("E-mail: support@nordicid.com")
			node.Expand()
		End If
	End Sub

	''' <summary>
	''' Dumps the object.
	''' </summary>
	''' <param name="node">The node.</param>
	''' <param name="value">The object.</param>
	Private Sub DumpObject(node As TreeNode, value As Object, title As String)
		Dim type As Type = value.[GetType]()
		Dim fi As FieldInfo() = type.GetFields(BindingFlags.[Public] Or BindingFlags.Instance)
		If fi.Length > 0 Then
			Dim thisnode As TreeNode = node.Nodes.Add(If(String.IsNullOrEmpty(title), type.Name, title))
			For Each info As FieldInfo In fi
				If info.FieldType.IsNested Then
					DumpObject(thisnode.Nodes.Add(info.Name), info.GetValue(value), Nothing)
				ElseIf info.FieldType.IsArray Then
					Dim arr As Array = DirectCast(info.GetValue(value), Array)
					Dim subnode As TreeNode = thisnode.Nodes.Add(String.Format("{0} {1}", info.Name, arr.Length))
					For i As Integer = 0 To arr.Length - 1
						Dim o As Object = arr.GetValue(i)
						DumpObject(subnode, o, "Value " & i.ToString())
					Next
				Else
					thisnode.Nodes.Add(info.Name & " = " & Convert.ToString(info.GetValue(value)))
				End If
			Next
		Else
			node.Nodes.Add(title & " = " & value.ToString())
		End If
	End Sub

	Private Sub AddExceptionNode(node As TreeNode, ex As NurApiException, colorize As Boolean)
		'TreeNode newnode;
		'if (ex.error == NurApiErrors.NUR_ERROR_INVALID_COMMAND)
		'    newnode = node.Nodes.Add(ex.TargetSite.ToString() + " - NOT SUPPORTED");
		'else
		'    newnode = node.Nodes.Add(ex.Message);
		'if (colorize)
		'{
		'    newnode.ForeColor = System.Drawing.Color.Red;
		'    node.Expand();
		'}
	End Sub

	Private Function TxLevel2mW(txLevel As Integer, max_dBm As Double) As String
		Dim mW As Integer = CInt(Math.Truncate(Math.Round(Math.Pow(10, (max_dBm - txLevel) / 10))))
		Return mW.ToString() & " mW"
	End Function

	Private Function GetEnabledAntennas(antennaMask As Integer) As String
		Dim sb As New StringBuilder()
		Dim index As Integer = 0
		While antennaMask <> 0
			If (antennaMask And 1) <> 0 Then
				If sb.Length > 0 Then
					sb.Append(", ")
				End If
				sb.Append(index.ToString())
			End If
			index += 1
			antennaMask = antennaMask >> 1
		End While
		Return sb.ToString()
	End Function

	<DllImport("NurApi.dll", CharSet := CharSet.Unicode, SetLastError := True, CallingConvention := CallingConvention.Winapi)> _
	Public Shared Function NurApiGetVersions(hApi As IntPtr, ByRef pMode As Byte, primary As StringBuilder, secondary As StringBuilder) As Integer
	End Function

	''' <summary>
	''' Returns the current mode, primary and secondary version information.
	''' The NurApiDotNet.dll version 1.6.1 (and older) have a bug in
	''' NurApi.GetVersions method. This workaround fix the issue.
	''' </summary>
	Public Function GetVersions(hNur As NurApi) As NurApi.ModuleVersions
		Dim [error] As Integer
		Dim modeByte As Byte = 0

		Dim ver As New NurApi.ModuleVersions()
		ver.mode = "Z"C

		Dim prim As New StringBuilder(100)
		Dim sec As New StringBuilder(100)

		[error] = NurApiGetVersions(hNur.GetHandle(), modeByte, prim, sec)

		If [error] <> 0 Then
			Throw New NurApiException("GetVersions", [error])
		End If

		ver.mode = ChrW(modeByte)
		ver.primaryVersion = prim.ToString()
		ver.secondaryVersion = sec.ToString()

		Return ver
	End Function

	''' <summary>
	''' Measures the Reflected Powers from enabled antennas.
	''' </summary>
	''' <param name="hNur">The NUR module handler.</param>
	''' <param name="node">The TreeView node.</param>
	''' <returns>True is Poor Antenna Found</returns>
	Private Function MeasureReflectedPowers(hNur As NurApi, node As TreeNode) As Boolean
		' Measure Reflected Power
		Dim antenna As Integer = 0
		Dim mask As Integer = hNur.AntennaMask
		Dim poorAntennaFound As Boolean = True
		If mask > 0 Then
			Dim tmpSelectdeAntenna As Integer = hNur.SelectedAntenna
			While mask > 0
				If (mask And 1) <> 0 Then
					Try
						' Select Antenna
						hNur.SelectedAntenna = antenna
						' Measure Reflected Power
						Dim regionInfo As NurApi.RegionInfo = hNur.GetRegionInfo()
						hNur.GetReflectedPower(regionInfo.baseFrequency)
						' FW bug fix
						Dim rpInfo As NurApi.ReflectedPowerInfo = hNur.GetReflectedPower()
						' Calculate Reflected Power
						Dim rf As Double = Math.Sqrt(CDbl(rpInfo.iPart * rpInfo.iPart + rpInfo.qPart * rpInfo.qPart))
						rf /= CDbl(rpInfo.div)
						rf = Math.Log10(rf) * 20.0
						If [Double].IsInfinity(rf) Then
							rf = -30
						End If
						If rf < 0 Then
							node.Nodes.Add(String.Format("Antenna {0} = Reflected Power {1:0.0} dBm", antenna, rf))
						Else
							Dim newNode As TreeNode = node.Nodes.Add(String.Format("Antenna {0} = Reflected Power {1:0.0} dBm. Make sure that the antenna is connected.", antenna, rf))
							newNode.ForeColor = System.Drawing.Color.Red
							node.Expand()
							poorAntennaFound = True
						End If
					Catch generatedExceptionName As NurApiException
						Dim newNode As TreeNode = node.Nodes.Add(String.Format("Antenna {0} = Could not measure the Reflected Power.", antenna))
						newNode.ForeColor = System.Drawing.Color.Red
						node.Expand()
						poorAntennaFound = True
					End Try
				End If
				mask = mask >> 1
				antenna += 1
			End While
			' Restore SelectedAntenna
			hNur.SelectedAntenna = tmpSelectdeAntenna
		Else
			Dim newNode As TreeNode = node.Nodes.Add("No eabled antennas!")
			newNode.ForeColor = System.Drawing.Color.Red
			poorAntennaFound = True
		End If
		Return poorAntennaFound
	End Function

	''' <summary>
	''' Refresh information.
	''' </summary>
	''' <param name="sender">The source of the event.</param>
	''' <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
	Private Sub tabControl1_Click(sender As Object, e As EventArgs)
		Dim tapControl As TabControl = TryCast(sender, TabControl)
		Dim hNur As NurApi = TryCast(tapControl.SelectedTab.Tag, NurApi)
		UpdateInfo(hNur)
	End Sub


	Private Sub saveInfoToXml_Button_Click(sender As Object, e As EventArgs)
		If saveFileDialog1.ShowDialog() = DialogResult.OK Then
			Dim nurSerializer As New NurSerializer()
			nurSerializer.Serialize(hNur)
			nurSerializer.SaveToFile(saveFileDialog1.FileName)
		End If
	End Sub
End Class
